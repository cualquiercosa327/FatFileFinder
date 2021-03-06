﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data;
using System.Xaml;
using System.IO;
using System.Xml;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using System.Diagnostics;

namespace FatFileFinder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //window constructor
        List<FolderDisplay> displayList;
        System.Threading.Thread bgWorker;
        string sidebarPath = "";
        FolderData sidebarFolder;
        public MainWindow()
        {
            InitializeComponent();
            displayList = new List<FolderDisplay>();
        }

       /// <summary>
       /// Sizes the folder tree with the given root folder on a background thread
       /// </summary>
       /// <param name="path">String representing the fully qualified path to the root folder</param>
        private void sizeFolder(string path)
        {
            FolderData fd = new FolderData(path,true);
            displayList = new List<FolderDisplay>();
            bgWorker = new System.Threading.Thread(() => fd.size(UpdateUIOnCallback));
            sidebarPath = path;
            revealButton.IsEnabled = true;
            RevealToolbar.IsEnabled = true;
            copyPath.IsEnabled = true;
            bgWorker.Start();
            //hide instructions label
            ContentGrid.Children.Remove(LInstructions);
        }

       /// <summary>
       /// Updates the UI on progress updates from the background thread
       /// </summary>
       /// <param name="fd">The FolderData object passed on the callback</param>
       /// <param name="prog">Double from 0-1 representing the current progress</param>
       /// <returns>true</returns>
        private bool UpdateUIOnCallback(FolderData fd, double prog)
        {   //if first update show UI
            if (fd.subFolders.Count() == 1)
            {
                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    FolderDisplay fd1 = new FolderDisplay(fd, 0);
                    addToTable(fd1);
                    //sign up the object's click event
                    fd1.GridClicked += OnGridClicked;
                    fd1.GridSingleClick += onSingleClick;
                    fd1.GridKeys += onGridKey;
                });
            }
            //update progress bar
            System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
            {
                MainProg.Value = prog;
                StatusL.Content = "Sizing " + fd.ToString() + " (" + Math.Round(prog * 100) + "%)";
                //loop through displaylist to get the correct folderdisplays
                FolderData root = fd;
                //if displaylist is empty (e.g. no subfolders), create one
                if (displayList.Count == 0)
                {
                    FolderDisplay fdisp = new FolderDisplay(root, 0);
                    fdisp.GridClicked += OnGridClicked;
                    fdisp.GridSingleClick += onSingleClick;
                    fdisp.GridKeys += onGridKey;
                    displayList.Add(fdisp);

                }
                displayList[0].folderData = root;
                displayList[0].updateTableListing();
                for (int i = 1; i < displayList.Count(); i++)
                {
                    //get the current one
                    FolderData fdt = displayList[i].folderData;

                    //find the correspondant in root
                    FolderData updated = root.sfDict[fdt.path.Name];

                    //update the list
                    displayList[i].folderData = updated;
                    displayList[i].updateTableListing();
                    //update root
                    root = updated;

                }
                //update the UI
                ContentGrid.Children.Clear();
                ContentGrid.ColumnDefinitions.Clear();
                foreach (FolderDisplay fdisp in displayList)
                {
                    addToTable(fdisp, false);
                }

                if (prog == 1)
                {
                    ChooseFolderBtn.IsEnabled = true;
                    ResizeFolder.IsEnabled = true;
                    StopBtn.IsEnabled = false;
                    sidebarFolder = fd;
                    updateSidebar();
                }
            });

            return true;
        }

        /// <summary>
        /// Called when a grid row is double clicked (event handler for event raised in FolderDisplay class)
        /// </summary>
        /// <param name="sender">FolderDisplay object that raised the event</param>
        /// <param name="e">Event arguments for the event</param>
        protected void OnGridClicked(object sender, EventArgs e)
        {
            FolderDisplayEvent fde = (FolderDisplayEvent)e;
            //check the fde.fileInfo and fde.folderData to see which to do
            if (fde.fileInfo != null)
            {
                updateSidebar();
            }
            else
            {
                //open another panel
                FolderDisplay fds = (FolderDisplay)sender;

                //clear the list as needed
                clearTableToLevel(fds.level); 
                
                //display the new folder display and register click handler
                FolderDisplay sub = new FolderDisplay(fde.folderData, fds.level + 1);
                addToTable(sub);
                sub.GridClicked += OnGridClicked;
                sub.GridSingleClick += onSingleClick;
                sub.GridKeys += onGridKey;
                     
            }
        }

        /// <summary>
        /// Called when a row in the grid is single clicked (for updating the sidebar)
        /// </summary>
        /// <param name="sender">FolderDisplay object that raised the event</param>
        /// <param name="e">Event arguments for the event</param>
        protected void onSingleClick(object sender, EventArgs e)
        {
            FolderDisplayEvent fde = (FolderDisplayEvent)e;
            //update the sidebarPath (folder or file?)
            if (fde.fileInfo != null)
            {
                sidebarPath = fde.fileInfo.FullName;
            }
            else
            {
                sidebarPath = fde.folderData.path.FullName;
                sidebarFolder = fde.folderData;
            }

            //redraw the sidebar
            updateSidebar();
        }

        /// <summary>
        /// Called when the user presses a key and the key press cannot be properly handled by the FolderDisplay object (transferring focus, etc)
        /// </summary>
        /// <param name="sender">FolderDisplay object that raised the event</param>
        /// <param name="e">KeyEventArgs for key press</param>
        protected void onGridKey(object sender, EventArgs e)
        {
            KeyEventArgs ke = (KeyEventArgs)e;
            //Enter key = open file in explorer
            if (ke.Key == Key.Enter)
            {
                explorerHereClicked(sender,new RoutedEventArgs());
            }
            //left or right arrow to change focus
          /*  else if (ke.Key == Key.Left)
            {
               FolderDisplay fd = (FolderDisplay)(sender);
               clearTableToLevel(fd.level);
            }
            else if (ke.Key == Key.Right)
            {
                //transfer focus
                FolderDisplay fd = (FolderDisplay)sender;
                if (fd.level < displayList.Count-1) {
                    DataGrid dtemp = displayList[fd.level + 1].dg;
                    //set selected item
                    if (dtemp.Items.Count > 0)
                    {
                        DataGridCellInfo dgc = new DataGridCellInfo(dtemp.Items[0], dtemp.Columns[0]);
                        dtemp.CurrentCell = dgc;
                        //setfocus
                        FocusManager.SetFocusedElement(dtemp, dgc);
                        dtemp.BeginEdit();
                    }
                }

            }*/
        }

        /// <summary>
        /// Updates the sidebar with attributes about the file
        /// Uses the global sidebarPath variable to determine which file to display properties for
        /// </summary>
        private void updateSidebar()
        {
            //Get fileattributes
            FileAttributes fa = File.GetAttributes(sidebarPath);
            FileSystemInfo data;
            if (fa.HasFlag(FileAttributes.Directory))
            {
                data = new DirectoryInfo(sidebarPath);
                FtypeL.Content = "Folder";
                FsizeL.Content = FolderDisplay.formatSize(sidebarFolder.total_size);
                FitemsL.Content = sidebarFolder.num_items;

            }
            else
            {
                data = new FileInfo(sidebarPath);
                FtypeL.Content = data.Extension + " file";
                FileInfo fi = (FileInfo)data;
                FsizeL.Content = FolderDisplay.formatSize(fi.Length);
                FitemsL.Content = "";
            }

            FnameL.Content = data.Name;
            FmodifiedL.Content = data.LastWriteTime;
            FsystemL.Content = fa.HasFlag(FileAttributes.System);
            FhiddenL.Content = fa.HasFlag(FileAttributes.Hidden);
            FcreatedL.Content = data.CreationTime;
            FreadonlyL.Content = fa.HasFlag(FileAttributes.ReadOnly);
            FtempL.Content = fa.HasFlag(FileAttributes.Temporary);
            FpathT.Text = data.FullName;
        }

        /// <summary>
        /// Adds a FolderDisplay object to the UI's column view
        /// </summary>
        /// <param name="fd">FolderDisplay object to add</param>
        /// <param name="readd">Whether this FolderDisplay should be re-added to the displayList</param>
        private void addToTable(FolderDisplay fd, bool readd = true)
        {
            //create the grid separator
            /*ContentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5) });
            GridSplitter gs = new GridSplitter() { Width = 5 };
            ContentGrid.Children.Add(gs);
            Grid.SetColumn(gs, ContentGrid.ColumnDefinitions.Count - 1);
            Grid.SetRow(gs, 1);*/

            //add the datagrid
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1,GridUnitType.Star) });
            ContentGrid.Children.Add(fd.dg);
            Grid.SetRow(fd.dg, 1);
            Grid.SetColumn(fd.dg, ContentGrid.ColumnDefinitions.Count - 1);

            Label l = new Label() { Content = fd.folderData.path.Name + " - " + fd.totalSizeFormatted() };
            ContentGrid.Children.Add(l);
            Grid.SetRow(l, 0);
            Grid.SetColumn(l, ContentGrid.ColumnDefinitions.Count - 1);

            if (readd)
            {
                displayList.Add(fd);
            }

            //scroll to end
            MainScrollView.ScrollToHorizontalOffset(System.Int32.MaxValue);
            MainScrollView.UpdateLayout();
        }

        /// <summary>
        /// Removes columns from the column view to a certain level, starting from the end
        /// </summary>
        /// <example>
        /// clearTableToLevel(5); //removes all the columns after the 5th column
        /// </example>
        /// <param name="level">Level to clear to (zero based)</param>
        private void clearTableToLevel(int level)
        {

            //displayList.RemoveRange(level, displayList.Count);
            //remove the ending N from the displayList
            for (int i = displayList.Count - 1; i > level; i--)
            {
                //remove from list
                displayList.Remove(displayList[i]);
            }

            //clear grid
            ContentGrid.Children.Clear();
            ContentGrid.ColumnDefinitions.Clear();

            //re-add what's left in displayList
            for (int i = 0; i < displayList.Count; i++)
            {
                addToTable(displayList[i], false);
            }
        }


        /// <summary>
        /// Called when the Stop button is pressed. Cancels any work the BGworker is doing
        /// </summary>
        /// <param name="sender">Button that raised the event</param>
        /// <param name="e">RoutedEventArgs from the button press</param>
        private void suspend_clicked(object sender, RoutedEventArgs e)
        {
            if (bgWorker != null)
            {
                StatusL.Content = "Sizing stopped";
                bgWorker.Abort();
                StopBtn.IsEnabled = false;
                ChooseFolderBtn.IsEnabled = true;
                ResizeFolder.IsEnabled = true;
            }
        }

        /// <summary>
        /// Called when the choose folder button is pressed
        /// </summary>
        /// <param name="sender">Button that raised the event</param>
        /// <param name="e">RoutedEventArgs from the button</param>
        private void chooseClicked(object sender, RoutedEventArgs e)
        {
            //FolderBrowser sucks, OpenFileDialog is much better but doesn't allow folder picking
            Forms.FolderBrowserDialog ofd = new Forms.FolderBrowserDialog();
            ofd.Description = "Select the folder to size";
            ofd.ShowNewFolderButton = false;
            if (ofd.ShowDialog() == Forms.DialogResult.OK)
            {
                clearTableToLevel(0);
                sizeFolder(ofd.SelectedPath);
                ChooseFolderBtn.IsEnabled = false;
                ResizeFolder.IsEnabled = false;
                StopBtn.IsEnabled = true;
            }
        }

        /// <summary>
        /// Refreshes the selected folder in the UI on a background thread. Has special case code.
        /// Uses the value stored in the sidebarFolder to choose the folder to resize.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="e">Arguments about the event</param>
        private void resizeFolder(object sender, RoutedEventArgs e)
        {
            if (sidebarFolder == null)
            {
                return;
            }
            //reinitialize background worker
            bgWorker = new System.Threading.Thread(() =>
            {
                bool onProgressUpdate(FolderData fd, double prog)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        if (prog == 1 && sidebarPath.Equals(fd.path.FullName))
                        {
                            updateSidebar();
                            StopBtn.IsEnabled = false;
                            //calculate the level
                            int lev = sidebarFolder.path.FullName.Split(System.IO.Path.DirectorySeparatorChar).Length - displayList[0].folderData.path.FullName.Split(System.IO.Path.DirectorySeparatorChar).Length - 1;
                            if (lev < 0) { lev = 0; }
                            //if this is the root folder, don't add a second collumn when updating UI
                            if (fd.root)
                            {
                                lev = -1;
                            }
                            //call the grid clicked event to update the table views
                            OnGridClicked(new FolderDisplay(sidebarFolder, lev), new FolderDisplayEvent() { folderData = sidebarFolder });
                            ResizeFolder.IsEnabled = true;
                        }
                        StatusL.Content = "Refreshing " + sidebarPath + " (" + Math.Round(prog * 100) + "%)";
                        MainProg.Value = prog;

                        //update the UI in the table
                        foreach (FolderDisplay f in displayList)
                        {
                            if (f.folderData.path.FullName.Equals(fd.path.FullName))
                            {
                                f.folderData = fd;
                                f.updateTableListing();
                            }
                        }

                    });

                    return true;
                }
                //resize the folder
                sidebarFolder.size(onProgressUpdate);
            });

            bgWorker.Start();
            StopBtn.IsEnabled = true;
            ResizeFolder.IsEnabled = false;
        }

        /// <summary>
        /// Opens the selected folder in File Explorer. If a file is selected, the containing folder is revealed.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="e">Event arguments</param>
        private void explorerHereClicked(object sender, RoutedEventArgs e)
        {
            FileAttributes fa = File.GetAttributes(sidebarPath);
            if (fa.HasFlag(FileAttributes.Directory))
            {
                Process.Start("explorer.exe", sidebarPath);
            }
            else
            {
                Process.Start("explorer.exe", new FileInfo(sidebarPath).Directory.FullName);
            }
        }

        /// <summary>
        /// Called when the Copy Path button is clicked. Copies the full path to the selected item to the clipboard
        /// </summary>
        /// <param name="sender">Button that raised event</param>
        /// <param name="e">Event arguments</param>
        private void copyPathClicked(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(sidebarPath);
        }
    }

    //for the table with the listings
    public class dataEntry
    {
        //Name of the item (file name)
        public string Name { get; set; }
        //percentage of the folder this item occupies
        public double Percentage { get; set; }
        //formatted file size
        public string Size { get; set; }
    }
}
