using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace UninstallerPro
{
    public static class Theme
    {
        public static ResourceDictionary Resources;

        private const string XamlTemplate = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                     xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <SolidColorBrush x:Key='BgBrush' Color='{BG}'/>
  <SolidColorBrush x:Key='PanelBrush' Color='{PANEL}'/>
  <SolidColorBrush x:Key='Panel2Brush' Color='{PANEL2}'/>
  <SolidColorBrush x:Key='HeaderBgBrush' Color='{HEADERBG}'/>
  <SolidColorBrush x:Key='HeaderTextBrush' Color='{HEADERTEXT}'/>
  <SolidColorBrush x:Key='HeaderSubTextBrush' Color='{HEADERSUB}'/>
  <SolidColorBrush x:Key='AccentBrush' Color='{ACCENT}'/>
  <SolidColorBrush x:Key='AccentHoverBrush' Color='{ACCENTHOVER}'/>
  <SolidColorBrush x:Key='AccentLightBrush' Color='{ACCENTLIGHT}'/>
  <SolidColorBrush x:Key='TextBrush' Color='{TEXT}'/>
  <SolidColorBrush x:Key='TextMutedBrush' Color='{TEXTMUTED}'/>
  <SolidColorBrush x:Key='DangerBrush' Color='{DANGER}'/>
  <SolidColorBrush x:Key='DangerHoverBrush' Color='{DANGERHOVER}'/>
  <SolidColorBrush x:Key='BorderColorBrush' Color='{BORDER}'/>
  <SolidColorBrush x:Key='GridAltBrush' Color='{GRIDALT}'/>
  <SolidColorBrush x:Key='WhiteBrush' Color='{ONDANGER}'/>
  <SolidColorBrush x:Key='AccentTextBrush' Color='{ACCENTTEXT}'/>
  <SolidColorBrush x:Key='HoverBgBrush' Color='{HOVERBG}'/>
  <SolidColorBrush x:Key='HoverTextBrush' Color='{HOVERTEXT}'/>
  <SolidColorBrush x:Key='FocusRingBrush' Color='{FOCUS}'/>

  <!-- Keyboard focus ring (WCAG 2.2 / STANDARDS.md 18.2). WPF's default
       FocusVisualStyle is a 1px dotted line in SystemColors.ControlText
       (black), which is effectively invisible on the Dark theme's #0F172A
       background and on every accent-filled button. This draws a 2px ring in
       a per-theme color chosen for >= 3:1 against that theme's background,
       offset just outside the control so it never sits on the fill. -->
  <Style x:Key='FocusRingStyle'>
    <Setter Property='Control.Template'>
      <Setter.Value>
        <ControlTemplate>
          <Rectangle Margin='-3' RadiusX='10' RadiusY='10' StrokeThickness='2' Stroke='{StaticResource FocusRingBrush}' SnapsToDevicePixels='True'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='BaseButtonStyle' TargetType='Button'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Height' Value='36'/>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRingStyle}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='Bd' CornerRadius='8' Background='{TemplateBinding Background}' SnapsToDevicePixels='True'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='10,0,10,0'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='Bd' Property='Opacity' Value='0.82'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='Bd' Property='Opacity' Value='0.5'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='GhostButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='{StaticResource Panel2Brush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource HoverBgBrush}'/>
        <Setter Property='Foreground' Value='{StaticResource HoverTextBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='AccentButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='{StaticResource AccentBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource AccentTextBrush}'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource AccentHoverBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='DangerButtonStyle' TargetType='Button' BasedOn='{StaticResource BaseButtonStyle}'>
    <Setter Property='Background' Value='{StaticResource DangerBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource WhiteBrush}'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'>
        <Setter Property='Background' Value='{StaticResource DangerHoverBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='ModernTabItemStyle' TargetType='TabItem'>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Padding' Value='18,10,18,10'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TabItem'>
          <Border x:Name='Bd' CornerRadius='10,10,0,0' Margin='2,4,2,0' Background='{StaticResource Panel2Brush}'>
            <ContentPresenter x:Name='Cp' ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='{TemplateBinding Padding}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentBrush}'/>
              <Setter Property='Foreground' Value='{StaticResource AccentTextBrush}'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='False'>
              <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ModernTabControlStyle' TargetType='TabControl'>
    <Setter Property='Background' Value='{StaticResource BgBrush}'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='0'/>
    <Setter Property='ItemContainerStyle' Value='{StaticResource ModernTabItemStyle}'/>
  </Style>

  <Style x:Key='ModernDataGridStyle' TargetType='DataGrid'>
    <Setter Property='Background' Value='{StaticResource PanelBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='RowBackground' Value='{StaticResource PanelBrush}'/>
    <Setter Property='AlternatingRowBackground' Value='{StaticResource GridAltBrush}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='BorderBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='GridLinesVisibility' Value='Horizontal'/>
    <Setter Property='HorizontalGridLinesBrush' Value='{StaticResource BorderColorBrush}'/>
    <Setter Property='RowHeaderWidth' Value='0'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='AutoGenerateColumns' Value='False'/>
    <Setter Property='CanUserAddRows' Value='False'/>
    <Setter Property='CanUserDeleteRows' Value='False'/>
    <Setter Property='IsReadOnly' Value='True'/>
    <Setter Property='SelectionMode' Value='Single'/>
    <Setter Property='SelectionUnit' Value='FullRow'/>
    <Setter Property='HeadersVisibility' Value='Column'/>
    <Setter Property='RowHeight' Value='32'/>
  </Style>

  <Style x:Key='ModernColumnHeaderStyle' TargetType='DataGridColumnHeader'>
    <Setter Property='Background' Value='{StaticResource AccentBrush}'/>
    <Setter Property='Foreground' Value='{StaticResource AccentTextBrush}'/>
    <Setter Property='FontWeight' Value='Bold'/>
    <Setter Property='Padding' Value='10,8,10,8'/>
    <Setter Property='HorizontalContentAlignment' Value='Left'/>
    <Setter Property='BorderThickness' Value='0,0,0,0'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
  </Style>

  <Style x:Key='ModernRowStyle' TargetType='DataGridRow'>
    <Setter Property='Padding' Value='0'/>
  </Style>

  <Style x:Key='ModernCellStyle' TargetType='DataGridCell'>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='10,4,10,4'/>
    <Style.Triggers>
      <Trigger Property='IsSelected' Value='True'>
        <Setter Property='Background' Value='{StaticResource AccentLightBrush}'/>
        <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='CardCheckBoxStyle' TargetType='CheckBox'>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRingStyle}'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
  </Style>

  <Style x:Key='SectionLabelStyle' TargetType='TextBlock'>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='15'/>
    <Setter Property='FontWeight' Value='Bold'/>
    <Setter Property='Foreground' Value='{StaticResource TextBrush}'/>
    <Setter Property='Margin' Value='0,0,0,10'/>
  </Style>

</ResourceDictionary>";

        // Name of the theme last passed to Load() (the user's own choice from
        // Settings/onboarding - "Light", "Dark" or "HighContrast"), so other
        // windows (onboarding replay) can start from the real current value
        // instead of assuming "Light".
        public static string CurrentName = "Light";

        // True when Windows itself is in a High Contrast / Contrast theme
        // (Settings > Accessibility > Contrast themes). STANDARDS.md 20.2: a
        // user who turned this on picked their own colors on purpose, so the
        // app must not paint its brand palette over them - every brush below
        // is then taken from SystemColors instead, whatever theme the user
        // picked inside OptiGuard.
        public static bool IsSystemHighContrast
        {
            get { try { return SystemParameters.HighContrast; } catch { return false; } }
        }

        // True whenever the effective palette is a high-contrast one (the
        // system contrast theme, or OptiGuard's own "High Contrast" choice) -
        // used to drop soft shadows/gradients that blur edges.
        public static bool IsAnyHighContrast
        {
            get { return IsSystemHighContrast || CurrentName == "HighContrast"; }
        }

        // First-run default when there is no saved choice yet: follow the
        // Windows app mode (Settings > Personalization > Colors > "Choose
        // your app mode"), per STANDARDS.md section 3 ("Dark+Light with
        // automatic detection from the system"). Falls back to Light if the
        // value can't be read.
        public static string DetectSystemTheme()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var v = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    if (v is int && (int)v == 0) return "Dark";
                }
            }
            catch { }
            return "Light";
        }

        private static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

        public static void Load(string themeName)
        {
            CurrentName = string.IsNullOrEmpty(themeName) ? "Light" : themeName;
            string xaml;
            if (IsSystemHighContrast)
            {
                // Windows contrast theme active: map every role onto the
                // user's own system colors. Pairs are always used the way
                // Windows guarantees contrast for them - WindowText on Window,
                // HighlightText on Highlight - never mixed across pairs.
                string window = Hex(SystemColors.WindowColor), windowText = Hex(SystemColors.WindowTextColor);
                string highlight = Hex(SystemColors.HighlightColor), highlightText = Hex(SystemColors.HighlightTextColor);
                xaml = XamlTemplate
                    .Replace("{BG}", window).Replace("{PANEL}", window).Replace("{PANEL2}", window)
                    .Replace("{HEADERBG}", window).Replace("{HEADERTEXT}", windowText).Replace("{HEADERSUB}", windowText)
                    .Replace("{ACCENT}", highlight).Replace("{ACCENTHOVER}", highlight).Replace("{ACCENTLIGHT}", window)
                    .Replace("{TEXT}", windowText).Replace("{TEXTMUTED}", windowText)
                    .Replace("{DANGER}", highlight).Replace("{DANGERHOVER}", highlight).Replace("{ONDANGER}", highlightText)
                    .Replace("{BORDER}", windowText).Replace("{GRIDALT}", window).Replace("{ACCENTTEXT}", highlightText)
                    .Replace("{HOVERBG}", highlight).Replace("{HOVERTEXT}", highlightText).Replace("{FOCUS}", highlight);
            }
            else if (CurrentName == "Dark")
            {
                // Accent/danger fills keep their brand hue but carry DARK text:
                // white on #10B981 is only 2.54:1 and white on #F87171 2.77:1
                // (WCAG AA needs 4.5:1) - #06281F on #10B981 is 6.2:1 and
                // #0F172A on #F87171 is 6.45:1.
                xaml = XamlTemplate
                    .Replace("{BG}", "#0F172A").Replace("{PANEL}", "#1E293B").Replace("{PANEL2}", "#273349")
                    .Replace("{HEADERBG}", "#0B1220").Replace("{HEADERTEXT}", "#F1F5F9").Replace("{HEADERSUB}", "#94A3B8")
                    .Replace("{ACCENT}", "#10B981").Replace("{ACCENTHOVER}", "#34D399").Replace("{ACCENTLIGHT}", "#0F3D30")
                    .Replace("{TEXT}", "#E2E8F0").Replace("{TEXTMUTED}", "#94A3B8")
                    .Replace("{DANGER}", "#F87171").Replace("{DANGERHOVER}", "#FCA5A5").Replace("{ONDANGER}", "#0F172A")
                    .Replace("{BORDER}", "#334155").Replace("{GRIDALT}", "#19233A").Replace("{ACCENTTEXT}", "#06281F")
                    .Replace("{HOVERBG}", "#334155").Replace("{HOVERTEXT}", "#E2E8F0").Replace("{FOCUS}", "#34D399");
            }
            else if (CurrentName == "HighContrast")
            {
                // ניגודיות גבוהה לנגישות: שחור-לבן טהור עם צהוב כצבע הדגשה - עומד
                // בדרישות WCAG AA/AAA להבחנה בין טקסט לרקע טוב יותר מהערכות הרגילות.
                // Hover is inverted yellow/black (it used to paint the white
                // Border color behind white text - invisible on hover).
                xaml = XamlTemplate
                    .Replace("{BG}", "#000000").Replace("{PANEL}", "#000000").Replace("{PANEL2}", "#1A1A1A")
                    .Replace("{HEADERBG}", "#000000").Replace("{HEADERTEXT}", "#FFFF00").Replace("{HEADERSUB}", "#FFFFFF")
                    .Replace("{ACCENT}", "#FFFF00").Replace("{ACCENTHOVER}", "#FFFFFF").Replace("{ACCENTLIGHT}", "#333300")
                    .Replace("{TEXT}", "#FFFFFF").Replace("{TEXTMUTED}", "#E0E0E0")
                    .Replace("{DANGER}", "#FF6B6B").Replace("{DANGERHOVER}", "#FF4040").Replace("{ONDANGER}", "#000000")
                    .Replace("{BORDER}", "#FFFFFF").Replace("{GRIDALT}", "#141414").Replace("{ACCENTTEXT}", "#000000")
                    .Replace("{HOVERBG}", "#FFFF00").Replace("{HOVERTEXT}", "#000000").Replace("{FOCUS}", "#00FFFF");
            }
            else
            {
                // Light: accent deepened from #10B981 to #047857 (same emerald
                // family) because #10B981 fails WCAG AA both as a fill under
                // white text (2.54:1) and as text on the light background
                // (2.32:1); #047857 is 5.48:1 / 5.01:1. Muted text #64748B ->
                // #475569 (was 4.34:1 on the page background, 4.08:1 on
                // Panel2) and danger #EF4444 -> #DC2626 (was 3.76:1).
                xaml = XamlTemplate
                    .Replace("{BG}", "#F1F5F9").Replace("{PANEL}", "#FFFFFF").Replace("{PANEL2}", "#E9EEF6")
                    .Replace("{HEADERBG}", "#0F2E27").Replace("{HEADERTEXT}", "#FFFFFF").Replace("{HEADERSUB}", "#8FBFB0")
                    .Replace("{ACCENT}", "#047857").Replace("{ACCENTHOVER}", "#065F46").Replace("{ACCENTLIGHT}", "#D1FAE5")
                    .Replace("{TEXT}", "#1E293B").Replace("{TEXTMUTED}", "#475569")
                    .Replace("{DANGER}", "#DC2626").Replace("{DANGERHOVER}", "#B91C1C").Replace("{ONDANGER}", "#FFFFFF")
                    .Replace("{BORDER}", "#E2E8F0").Replace("{GRIDALT}", "#F8FAFC").Replace("{ACCENTTEXT}", "#FFFFFF")
                    .Replace("{HOVERBG}", "#E2E8F0").Replace("{HOVERTEXT}", "#1E293B").Replace("{FOCUS}", "#0F2E27");
            }
            using (var stringReader = new StringReader(xaml))
            using (var xmlReader = System.Xml.XmlReader.Create(stringReader))
            {
                Resources = (ResourceDictionary)XamlReader.Load(xmlReader);
            }
        }

        public static Brush Get(string key) { return (Brush)Resources[key]; }
        public static object GetStyle(string key) { return Resources[key]; }

        // צל עדין לכרטיסי-מפתח (ציון בריאות, אשפים) - עומק ויזואלי בלי לגעת
        // בעיצוב הבסיסי של כל כרטיס אחר באפליקציה. מבוטל בניגודיות גבוהה כי
        // צל רך פוגע בחדות הגבולות שהמצב הזה נועד להבטיח.
        public static System.Windows.Media.Effects.Effect CardShadow(string themeName)
        {
            if (themeName == "HighContrast" || IsSystemHighContrast) return null;
            return new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, Opacity = themeName == "Dark" ? 0.35 : 0.12, BlurRadius = 18, ShadowDepth = 3, Direction = 270
            };
        }
    }
}
