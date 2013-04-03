﻿#region license
// /*
//     This file is part of Naming Fix AddIn.
// 
//     Naming Fix AddIn is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     Naming Fix AddIn is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with Naming Fix AddIn. If not, see <http://www.gnu.org/licenses/>.
//  */
#endregion

using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace NamingFix
{
    /// <summary>
    ///     This is the class that implements the package exposed by this assembly.
    ///     The minimum requirement for a class to be considered a valid package for Visual Studio
    ///     is to implement the IVsPackage interface and register itself with the shell.
    ///     This package uses the helper classes defined inside the Managed Package Framework (MPF)
    ///     to do it: it derives from the Package class that provides the implementation of the
    ///     IVsPackage interface and uses the registration attributes defined in the framework to
    ///     register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true), InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400), ProvideMenuResource("Menus.ctmenu", 1),
     Guid(GuidList.GuidNamingFixPkgString)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    // This attribute is needed to let the shell know that this package exposes some menus.
    public sealed class CNamingFixPackage : Package
    {
        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation

        #region Package Members
        private CNamingFix _NamingFix;

        /// <summary>
        ///     Initialization of the package; this method is called right after the package is sited, so this is the place
        ///     where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            DTE2 dte = GetGlobalService(typeof(DTE)) as DTE2;

            if (_NamingFix == null)
                _NamingFix = new CNamingFix(dte);

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null == mcs)
                return;
            // Create the command for the menu item.
            CommandID menuCommandId = new CommandID(GuidList.GuidNamingFixCmdSet, (int)PkgCmdIDList.cmdApplyNameScheme);
            MenuCommand menuItem = new MenuCommand(_NamingFix.MenuItemCallback, menuCommandId);
            mcs.AddCommand(menuItem);
        }
        #endregion
    }
}