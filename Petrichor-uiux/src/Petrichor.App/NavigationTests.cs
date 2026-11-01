#if DEBUG
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Petrichor.App.Pages;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Petrichor.App
{
    // Exercises the real UWP Frame journal without a music account or network responses.
    internal static class NavigationTests
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static async Task RunAsync()
        {
            var results = new List<string>();
            string error = null;
            try
            {
                var shell = Shell.Current;
                var details = (Frame)shell.FindName("ContentFrame");
                shell.Navigate(typeof(DiscoverPage));
                shell.Navigate(typeof(SettingsPage));
                Require(details.BackStackDepth == 0, "First detail has a hidden ancestor");
                shell.Navigate(typeof(QueuePage));
                Require(details.BackStackDepth == 1, "Nested detail lost its parent");
                shell.GoBack();
                Require(details.Content is SettingsPage, "Nested back skipped its parent");
                shell.GoBack();
                Require(details.Visibility == Visibility.Collapsed && details.BackStackDepth == 0,
                    "Detail root did not return to the home pivot");
                results.Add("nested detail -> parent -> home");

                shell.Navigate(typeof(DownloadsPage));
                await Task.Delay(50);
                Window.Current.Content.UpdateLayout();
                Require(details.BackStackDepth == 0, "Closed Settings route reappeared behind Downloads");
                var downloads = (DownloadsPage)details.Content;
                downloads.SetSelectionMode(true);
                shell.GoBack();
                Require(ReferenceEquals(details.Content, downloads) && details.Visibility == Visibility.Visible,
                    "Back exited the page instead of cancelling selection");
                Require(((ListView)downloads.FindName("Saved")).SelectionMode == ListViewSelectionMode.None,
                    "Back did not cancel multi-selection");
                shell.GoBack();
                Require(details.Visibility == Visibility.Collapsed, "Second back did not close Downloads");
                results.Add("multi-select -> same page -> home");

                shell.Navigate(typeof(SettingsPage));
                shell.Navigate(typeof(SearchPage));
                shell.Navigate(typeof(QueuePage));
                Require(details.BackStackDepth == 0, "A peer pivot kept an earlier detail route");
                shell.GoBack();
                Require(details.Visibility == Visibility.Collapsed, "Reopened detail returned to a stale route");
                Require(((Pivot)shell.FindName("RootPivot")).SelectedIndex == 1,
                    "Back returned to a different home pivot");
                results.Add("peer pivot -> new detail -> original pivot");
            }
            catch (Exception ex) { error = ex.ToString(); }
            var output = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                "navigation-regression.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(output, JsonConvert.SerializeObject(
                new { passed = error == null, checks = results, error = error }, Formatting.Indented));
        }
    }
}
#endif
