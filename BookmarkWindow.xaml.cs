using System.Windows;

namespace YuanShenTools
{
    /// <summary>
    /// 书签管理窗口（当前未使用，书签功能已集成到主窗口覆盖层）
    /// 保留以备后续可能改为独立窗口。
    /// </summary>
    public partial class BookmarkWindow : Window
    {
        /// <summary>当用户双击书签时触发，传入选中书签的 URL</summary>
        public event Action<string>? BookmarkSelected;

        public BookmarkWindow()
        {
            InitializeComponent();
            LoadBookmarks();
        }

        /// <summary>从配置加载书签列表</summary>
        private void LoadBookmarks()
        {
            var config = Config.Load();
            BookmarkList.Items.Clear();
            foreach (var url in config.Bookmarks)
            {
                BookmarkList.Items.Add(new BookmarkItem { Title = url, Url = url });
            }
        }

        /// <summary>添加书签（将当前输入框 URL 保存到配置）</summary>
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

        /// <summary>双击书签项触发导航</summary>
        private void BookmarkList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (BookmarkList.SelectedItem is BookmarkItem item)
            {
                BookmarkSelected?.Invoke(item.Url);
            }
        }

        /// <summary>删除选中书签</summary>
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

        /// <summary>书签项数据模型</summary>
        private class BookmarkItem
        {
            public required string Title { get; set; }
            public required string Url { get; set; }

            public override string ToString() => Title;
        }
    }
}
