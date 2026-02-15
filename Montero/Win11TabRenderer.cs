using EasyTabs;

namespace Montero
{
    internal sealed class Win11TabRenderer : ChromeTabRenderer
    {
        public Win11TabRenderer(TitleBarTabs parentWindow)
            : base(parentWindow)
        {
            ShowAddButton = true;
        }
    }
}
