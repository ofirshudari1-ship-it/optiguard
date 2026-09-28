using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UninstallerPro
{
    // First-run onboarding: 3 screens (reduced from 5 - the redesign brief
    // asked for fewer/shorter wizards app-wide). The old 5-page version had
    // only one page with anything to actually decide (the theme); the other
    // four were pure "read this, click Next" text screens, which is exactly
    // the kind of over-long wizard the redesign calls out. Welcome and
    // Features are now one combined intro page, and Tip and Finish are one
    // combined closing page - no information was dropped, just no longer
    // spread across a click per paragraph.
    // Skip is always visible (not hidden/greyed) and Esc skips too; whoever
    // skips still gets sane defaults (the theme already picked on the
    // screens they did see, or Light if they skip immediately - and the
    // language already set by the installer, see ResultLanguage below).
    // No language picker: the installer already asked once, so this wizard
    // never re-asks. Shown once - MainWindow sets FirstLaunchCompleted=true
    // after this closes and never shows it again.
    public class OnboardingWindow : Window
    {
        public string ResultLanguage { get; private set; }
        public string ResultTheme { get; private set; }

        private int _page = 0;
        private const int TotalPages = 3;

        private readonly ContentControl _pageHost = new ContentControl();
        private readonly StackPanel _dotsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        private Button _btnBack, _btnNext, _btnSkip;
        private ComboBox _cmbTheme;

        public OnboardingWindow()
        {
            ResultLanguage = I18n.CurrentLang;
            // Start from the theme actually in effect (system-detected on first
            // run, the saved choice on "Show welcome guide again") - a fixed
            // "Light" here silently switched Dark users back to Light when they
            // replayed the guide and skipped past the theme page.
            ResultTheme = Theme.CurrentName;

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

            // page 0 = Welcome + Features combined, page 1 = Theme (the only
            // page with an actual choice on it), page 2 = Tip + Finish
            // combined. Every string from the old 5-page version is still
            // shown in full - just two paragraphs per page instead of one.
            string titleKey;
            string bodyText;
            switch (page)
            {
                case 0:
                    titleKey = "onb_page_welcome_title";
                    bodyText = I18n.T("onb_page_welcome_body") + "\n\n" + I18n.T("onb_page_features_body");
                    break;
                case 1:
                    titleKey = "onb_page_theme_title";
                    bodyText = I18n.T("onb_page_theme_body");
                    break;
                default:
                    titleKey = "onb_page_finish_title";
                    bodyText = I18n.T("onb_page_tip_body") + "\n\n" + I18n.T("onb_page_finish_body");
                    break;
            }

            if (page == 0)
            {
                var illustration = BuildWelcomeIllustration();
                if (illustration != null) stack.Children.Add(illustration);
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
                Text = bodyText,
                FontSize = 14,
                Foreground = Theme.Get("TextMutedBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            if (page == 0)
            {
                // No language picker here on purpose: the installer already
                // asked for a language (or an existing settings.json already
                // has one) and I18n.CurrentLang is loaded from that before
                // this window is ever constructed (see ResultLanguage above,
                // and MainWindow's ctor which sets I18n.CurrentLang before
                // opening OnboardingWindow). Re-asking here would just be the
                // same question twice. Settings still has a full Language
                // picker for anyone who wants to double-check or change it.
                stack.Children.Add(new TextBlock
                {
                    Text = I18n.T("onb_language_note"),
                    FontSize = 12,
                    Foreground = Theme.Get("TextMutedBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 0)
                });
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

        // Welcome-page illustration (Bloom AI, see ../assets/onboarding/meta.json).
        // The source image has a plain light background, so on the Dark/HighContrast
        // themes it's set inside a fixed-white rounded card (never the theme's own
        // panel color) rather than pasted straight onto the page - otherwise it
        // would show as a jarring pale rectangle on the dark background instead of
        // looking like an intentional framed graphic. Theme.CardShadow already
        // returns null under high contrast, so the card stays a flat bordered
        // rectangle there instead of a soft shadow that would blur its edge.
        private static UIElement BuildWelcomeIllustration()
        {
            BitmapFrame frame;
            try
            {
                var resourceUri = new Uri("pack://application:,,,/onboarding-illustration.png", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(resourceUri);
                if (streamInfo == null) return null;
                using (streamInfo.Stream)
                {
                    frame = BitmapFrame.Create(streamInfo.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }
            catch
            {
                // Missing/corrupt resource must never block the onboarding flow -
                // the page still works fine with just the title and body text.
                return null;
            }

            var image = new Image
            {
                Source = frame,
                Height = 108,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = Theme.Get("BorderColorBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18, 14, 18, 14),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                Effect = Theme.CardShadow(Theme.CurrentName),
                Child = image
            };
            return card;
        }
    }
}
