using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UninstallerPro
{
    // First-run onboarding: 5 screens max, per the shared cross-tool standard.
    // Skip is always visible (not hidden/greyed) and Esc skips too; whoever
    // skips still gets sane defaults (the language/theme already picked on
    // the screens they did see, or English/Light if they skip immediately).
    // Shown once - MainWindow sets FirstLaunchCompleted=true after this closes
    // and never shows it again.
    public class OnboardingWindow : Window
    {
        public string ResultLanguage { get; private set; }
        public string ResultTheme { get; private set; }

        private int _page = 0;
        private const int TotalPages = 5;

        private readonly ContentControl _pageHost = new ContentControl();
        private readonly StackPanel _dotsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private Button _btnBack, _btnNext, _btnSkip;
        private ComboBox _cmbLang, _cmbTheme;

        public OnboardingWindow()
        {
            ResultLanguage = I18n.CurrentLang;
            ResultTheme = "Light";

            Width = 560;
            Height = 440;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.Get("BgBrush");
            Foreground = Theme.Get("TextBrush");

            KeyDown += (s, e) => { if (e.Key == Key.Escape) FinishOrSkip(skip: true); };

            BuildChrome();
            RenderPage();
        }

        private void BuildChrome()
        {
            var root = new DockPanel { Margin = new Thickness(28) };
            Content = root;

            DockPanel.SetDock(_dotsPanel, Dock.Top);
            root.Children.Add(_dotsPanel);

            var bottomGrid = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            DockPanel.SetDock(bottomGrid, Dock.Bottom);
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnSkip = new Button
            {
                Content = I18n.T("onb_btn_skip"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Theme.Get("TextMutedBrush"),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(8, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _btnSkip.Click += (s, e) => FinishOrSkip(skip: true);
            Grid.SetColumn(_btnSkip, 0);
            bottomGrid.Children.Add(_btnSkip);

            var navPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _btnBack = new Button { Content = I18n.T("onb_btn_back"), Width = 90, Height = 36, Margin = new Thickness(0, 0, 8, 0), Background = Theme.Get("PanelBrush"), Foreground = Theme.Get("TextBrush"), BorderThickness = new Thickness(1), BorderBrush = Theme.Get("BorderColorBrush") };
            _btnBack.Click += (s, e) => { if (_page > 0) { _page--; RenderPage(); } };
            _btnNext = new Button { Content = I18n.T("onb_btn_next"), Width = 130, Height = 36, Background = Theme.Get("AccentBrush"), Foreground = Theme.Get("AccentTextBrush"), BorderThickness = new Thickness(0) };
            _btnNext.Click += (s, e) =>
            {
                if (_page < TotalPages - 1) { _page++; RenderPage(); }
                else FinishOrSkip(skip: false);
            };
            navPanel.Children.Add(_btnBack);
            navPanel.Children.Add(_btnNext);
            Grid.SetColumn(navPanel, 2);
            bottomGrid.Children.Add(navPanel);

            root.Children.Add(bottomGrid);
            root.Children.Add(_pageHost);
        }

        private void FinishOrSkip(bool skip)
        {
            // Whatever was picked on the pages the user actually saw (language,
            // theme) is kept even on skip - only unvisited pages fall back to
            // the constructor's defaults (English/Light).
            DialogResult = null;
            Close();
        }

        private void RenderPage()
        {
            Title = I18n.T("app_name");
            FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _btnSkip.Content = I18n.T("onb_btn_skip");
            _btnBack.Content = I18n.T("onb_btn_back");
            _btnNext.Content = _page == TotalPages - 1 ? I18n.T("btn_get_started") : I18n.T("onb_btn_next");
            _btnBack.Visibility = _page == 0 ? Visibility.Hidden : Visibility.Visible;

            _dotsPanel.Children.Clear();
            for (int i = 0; i < TotalPages; i++)
            {
                _dotsPanel.Children.Add(new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(4, 0, 4, 0),
                    Background = i == _page ? Theme.Get("AccentBrush") : Theme.Get("BorderColorBrush")
                });
            }

            _pageHost.Content = BuildPageContent(_page);
        }

        private UIElement BuildPageContent(int page)
        {
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            string titleKey, bodyKey;
            switch (page)
            {
                case 0: titleKey = "onb_page_welcome_title"; bodyKey = "onb_page_welcome_body"; break;
                case 1: titleKey = "onb_page_theme_title"; bodyKey = "onb_page_theme_body"; break;
                case 2: titleKey = "onb_page_features_title"; bodyKey = "onb_page_features_body"; break;
                case 3: titleKey = "onb_page_tip_title"; bodyKey = "onb_page_tip_body"; break;
                default: titleKey = "onb_page_finish_title"; bodyKey = "onb_page_finish_body"; break;
            }

            stack.Children.Add(new TextBlock
            {
                Text = I18n.T(titleKey),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.Get("TextBrush"),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = I18n.T(bodyKey),
                FontSize = 14,
                Foreground = Theme.Get("TextMutedBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            if (page == 0)
            {
                stack.Children.Add(new TextBlock { Text = I18n.T("language_select_label"), Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 0, 0, 8) });
                _cmbLang = new ComboBox { Width = 220, Height = 34, HorizontalAlignment = HorizontalAlignment.Left };
                _cmbLang.Items.Add("English");
                _cmbLang.Items.Add("עברית");
                _cmbLang.SelectedIndex = ResultLanguage == I18n.Hebrew ? 1 : 0;
                _cmbLang.SelectionChanged += (s, e) =>
                {
                    ResultLanguage = _cmbLang.SelectedIndex == 1 ? I18n.Hebrew : I18n.English;
                    I18n.CurrentLang = ResultLanguage;
                    RenderPage();
                };
                stack.Children.Add(_cmbLang);
            }
            else if (page == 1)
            {
                stack.Children.Add(new TextBlock { Text = I18n.T("theme_select_label"), Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 0, 0, 8) });
                _cmbTheme = new ComboBox { Width = 220, Height = 34, HorizontalAlignment = HorizontalAlignment.Left };
                _cmbTheme.Items.Add(I18n.T("theme_light"));
                _cmbTheme.Items.Add(I18n.T("theme_dark"));
                _cmbTheme.Items.Add(I18n.T("theme_high_contrast"));
                _cmbTheme.SelectedIndex = ResultTheme == "Dark" ? 1 : (ResultTheme == "HighContrast" ? 2 : 0);
                _cmbTheme.SelectionChanged += (s, e) =>
                {
                    ResultTheme = _cmbTheme.SelectedIndex == 1 ? "Dark" : (_cmbTheme.SelectedIndex == 2 ? "HighContrast" : "Light");
                };
                stack.Children.Add(_cmbTheme);
            }
            else if (page == TotalPages - 1)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = I18n.T("copyright"),
                    FontSize = 11,
                    Foreground = Theme.Get("TextMutedBrush"),
                    Margin = new Thickness(0, 12, 0, 0)
                });
            }

            return stack;
        }
    }
}
