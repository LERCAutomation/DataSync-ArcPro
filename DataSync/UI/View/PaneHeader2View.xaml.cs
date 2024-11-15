// The DataTools are a suite of ArcGIS Pro addins used to extract
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs..
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using ArcGIS.Core.Data.UtilityNetwork.Trace;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace DataSync.UI
{
    /// <summary>
    /// Interaction logic for PaneHeader2View.xaml
    /// </summary>
    public partial class PaneHeader2View : System.Windows.Controls.UserControl
    {
        public PaneHeader2View()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Ensure any removed result summary items are actually unselected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultSummary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the list of removed items.
            List<ResultSummary> removed = e.RemovedItems.OfType<ResultSummary>().ToList();

            // Ensure any removed items are actually unselected.
            if (removed.Count > 1)
            {
                // Unselect the removed items.
                e.RemovedItems.OfType<ResultSummary>().ToList().ForEach(p => p.IsSelected = false);

                // Get the list of currently selected items.
                var listView = sender as System.Windows.Controls.ListView;
                var selectedItems = listView.SelectedItems.OfType<ResultSummary>().ToList();

                if (selectedItems.Count == 1)
                    listView.Items.OfType<ResultSummary>().ToList().Where(s => selectedItems.All(s2 => s2.Type != s.Type)).ToList().ForEach(p => p.IsSelected = false);
            }
        }

        /// <summary>
        /// Display the details when a result summary is double-clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultSummary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the original element that was double-clicked on
            // and search from child to parent until you find either
            // a ListViewItem or the top of the tree.
            DependencyObject originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && originalSource is not System.Windows.Controls.ListViewItem)
            {
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }

            // If it didn’t find a ListViewItem anywhere in the hierarchy
            // then it’s because the user didn’t click on one. Therefore
            // if the variable isn’t null, run the code.
            if (originalSource != null)
            {
                if (ListViewResultSummary.SelectedItem is ResultSummary resultSummary)
                {
                    //TODO - Change to load result details list
                    // Display the selected result summary details.
                    string strText = string.Format("{0}",
                        resultSummary.Type);
                    MessageBox.Show(strText, "Result Summary", MessageBoxButton.OK);
                }
            }
        }

        /// <summary>
        /// Ensure any removed result details are actually unselected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultDetail_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the list of removed items.
            List<ResultDetail> removed = e.RemovedItems.OfType<ResultDetail>().ToList();

            // Ensure any removed items are actually unselected.
            if (removed.Count > 1)
            {
                // Unselect the removed items.
                e.RemovedItems.OfType<ResultDetail>().ToList().ForEach(p => p.IsSelected = false);

                // Get the list of currently selected items.
                var listView = sender as System.Windows.Controls.ListView;
                var selectedItems = listView.SelectedItems.OfType<ResultDetail>().ToList();

                if (selectedItems.Count == 1)
                    listView.Items.OfType<ResultDetail>().ToList().Where(s => selectedItems.All(s2 => s2.NewRef != s.NewRef)).ToList().ForEach(p => p.IsSelected = false);
            }
        }

        /// <summary>
        /// Display the details when a result item is double-clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultDetail_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Get the original element that was double-clicked on
            // and search from child to parent until you find either
            // a ListViewItem or the top of the tree.
            DependencyObject originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && originalSource is not System.Windows.Controls.ListViewItem)
            {
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }

            // If it didn’t find a ListViewItem anywhere in the hierarchy
            // then it’s because the user didn’t click on one. Therefore
            // if the variable isn’t null, run the code.
            if (originalSource != null)
            {
                if (ListViewResultDetail.SelectedItem is ResultDetail resultDetail)
                {
                    //TODO - Change to zoom to feature in layer
                    string newRef = (string.IsNullOrEmpty(resultDetail.NewRef) ? string.Empty : "\r\nNew Ref : " + resultDetail.NewRef);
                    string oldRef = (string.IsNullOrEmpty(resultDetail.OldRef) ? string.Empty : "\r\n\r\nOld Ref : " + resultDetail.OldRef);
                    string newArea = (resultDetail.NewArea == "0") ? string.Empty : "\r\n\r\nNew Area : " + resultDetail.NewArea.ToString();
                    string oldArea = (resultDetail.OldArea == "0") ? string.Empty : "\r\n\r\nOld Area : " + resultDetail.OldArea.ToString();

                    // Display the selected result detail item.
                    string strText = string.Format("{0}\r\n\r\n{1}{2}{3}{4}",
                        resultDetail.Type, newRef, oldRef, newArea, oldArea);
                    MessageBox.Show(strText, "Result Details", MessageBoxButton.OK);
                }
            }
        }

        /// <summary>
        /// Return the first visual child object of the required type
        /// for the specified object.
        /// </summary>
        /// <typeparam name="childItem"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static childItem FindVisualChild<childItem>(DependencyObject obj)
               where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem item)
                    return item;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }

            return null;
        }
    }
}