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
  <SolidColorBrush x:Key='WhiteBrush' Color='#FFFFFF'/>
  <SolidColorBrush x:Key='AccentTextBrush' Color='{ACCENTTEXT}'/>

  <Style x:Key='BaseButtonStyle' TargetType='Button'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13.5'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Height' Value='36'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='Bd' CornerRadius='8' Background='{TemplateBinding Background}' SnapsToDevicePixels='True'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='10,0,10,0'/>
          </Border>
          <ControlTemplate.Triggers>
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
        <Setter Property='Background' Value='{StaticResource BorderColorBrush}'/>
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
    <Setter Property='HorizontalContentAlignment' Value='Right'/>
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

        public static void Load(string themeName)
        {
            string xaml;
            if (themeName == "Dark")
            {
                xaml = XamlTemplate
                    .Replace("{BG}", "#0F172A").Replace("{PANEL}", "#1E293B").Replace("{PANEL2}", "#273349")
                    .Replace("{HEADERBG}", "#0B1220").Replace("{HEADERTEXT}", "#F1F5F9").Replace("{HEADERSUB}", "#94A3B8")
                    .Replace("{ACCENT}", "#10B981").Replace("{ACCENTHOVER}", "#34D399").Replace("{ACCENTLIGHT}", "#0F3D30")
                    .Replace("{TEXT}", "#E2E8F0").Replace("{TEXTMUTED}", "#94A3B8")
                    .Replace("{DANGER}", "#F87171").Replace("{DANGERHOVER}", "#EF4444")
                    .Replace("{BORDER}", "#334155").Replace("{GRIDALT}", "#19233A").Replace("{ACCENTTEXT}", "#FFFFFF");
            }
            else if (themeName == "HighContrast")
            {
                // ניגודיות גבוהה לנגישות: שחור-לבן טהור עם צהוב כצבע הדגשה - עומד
                // בדרישות WCAG AA/AAA להבחנה בין טקסט לרקע טוב יותר מהערכות הרגילות.
                xaml = XamlTemplate
                    .Replace("{BG}", "#000000").Replace("{PANEL}", "#000000").Replace("{PANEL2}", "#1A1A1A")
                    .Replace("{HEADERBG}", "#000000").Replace("{HEADERTEXT}", "#FFFF00").Replace("{HEADERSUB}", "#FFFFFF")
                    .Replace("{ACCENT}", "#FFFF00").Replace("{ACCENTHOVER}", "#FFFFFF").Replace("{ACCENTLIGHT}", "#333300")
                    .Replace("{TEXT}", "#FFFFFF").Replace("{TEXTMUTED}", "#E0E0E0")
                    .Replace("{DANGER}", "#FF6B6B").Replace("{DANGERHOVER}", "#FF4040")
                    .Replace("{BORDER}", "#FFFFFF").Replace("{GRIDALT}", "#141414").Replace("{ACCENTTEXT}", "#000000");
            }
            else
            {
                xaml = XamlTemplate
                    .Replace("{BG}", "#F1F5F9").Replace("{PANEL}", "#FFFFFF").Replace("{PANEL2}", "#E9EEF6")
                    .Replace("{HEADERBG}", "#0F2E27").Replace("{HEADERTEXT}", "#FFFFFF").Replace("{HEADERSUB}", "#8FBFB0")
                    .Replace("{ACCENT}", "#10B981").Replace("{ACCENTHOVER}", "#059669").Replace("{ACCENTLIGHT}", "#D1FAE5")
                    .Replace("{TEXT}", "#1E293B").Replace("{TEXTMUTED}", "#64748B")
                    .Replace("{DANGER}", "#EF4444").Replace("{DANGERHOVER}", "#DC2626")
                    .Replace("{BORDER}", "#E2E8F0").Replace("{GRIDALT}", "#F8FAFC").Replace("{ACCENTTEXT}", "#FFFFFF");
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
            if (themeName == "HighContrast") return null;
            return new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, Opacity = themeName == "Dark" ? 0.35 : 0.12, BlurRadius = 18, ShadowDepth = 3, Direction = 270
            };
        }
    }
}
