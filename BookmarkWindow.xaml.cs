using System.Windows;

namespace YuanShenTools
{
    public partial class BookmarkWindow : Window
    {
        public event Action<string>? BookmarkSelected;

        public BookmarkWindow()
        {
            InitializeComponent();
            LoadBookmarks();
        }

        private void LoadBookmarks()
        {
            var config = Config.Load();
            BookmarkList.Items.Clear();
            foreach (var url in config.Bookmarks)
            {
                BookmarkList.Items.Add(new BookmarkItem { Title = url, Url = url });
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var url = NewUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            var config = Config.Load();
            if (!config.Bookmarks.Contains(url))
            {
                config.Bookmarks.Add(url);
                Config.Save(config);
                BookmarkList.Items.Add(new BookmarkItem { Title = url, Url = url });
            }
            NewUrlTextBox.Clear();
        }

        private void BookmarkList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (BookmarkList.SelectedItem is BookmarkItem item)
            {
                BookmarkSelected?.Invoke(item.Url);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkList.SelectedItem is BookmarkItem item)
            {
                var config = Config.Load();
                config.Bookmarks.Remove(item.Url);
                Config.Save(config);
                BookmarkList.Items.Remove(item);
            }
        }

        private class BookmarkItem
        {
            public required string Title { get; set; }
            public required string Url { get; set; }

            public override string ToString() => Title;
        }
    }
}
