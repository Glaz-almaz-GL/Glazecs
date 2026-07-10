using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace Glazecs.App.Desktop.Components.Layout
{
    public sealed partial class MainLayout : LayoutComponentBase
    {
        private bool _drawerOpen = false;

        public void DrawerToggle()
        {
            _drawerOpen = !_drawerOpen;
        }
    }
}
