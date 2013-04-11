#region license
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
using EnvDTE;
using EnvDTE80;

namespace NamingFix
{
    abstract class CRenameItemElement : CRenameItem
    {
        private vsCMElement _Kind;
        private ProjectItem _ProjectItem;
        private TextPoint _StartPoint;
        private string _CheckName;

        private CodeElement _Element;
        public CodeElement Element
        {
            private get { return _Element; }
            set
            {
                _Element = value;
                if (IsSystem)
                    return;
                _Kind = value.Kind;
                _ProjectItem = value.ProjectItem;
                _StartPoint = value.GetStartPoint(vsCMPart.vsCMPartNavigate);
                _CheckName = value.Name;
            }
        }

        private CodeElement GetFreshCodeElement()
        {
            return (IsSystem || Element == null) ? null : CUtils.GetCodeElementAtTextPoint(_StartPoint, _Kind, _ProjectItem);
        }

        protected T GetElement<T>()
        {
            return (T)Element;
        }

        public override ProjectItem ProjectItem
        {
            get { return _ProjectItem; }
        }
        public override TextPoint StartPoint
        {
            get { return _StartPoint; }
        }

        public bool RefreshElement()
        {
            CodeElement element = GetFreshCodeElement();
            if (element.Name == NewName)
            {
                Element = element;
                Name = NewName;
                return true;
            }
            if (element.Name != _CheckName)
            {
                CNamingFix.Message("!!!Wrong names: " + element.FullName + "!=" + _CheckName);
                return false;
            }
            Element = element;
            return true;
        }

        public override bool Rename()
        {
            if (NewName == Name)
                return true;
            CodeElement2 element = Element as CodeElement2;
            try
            {
                if (element != null)
                    element.RenameSymbol(NewName);
                Name = NewName;
                return true;
            }
            catch (COMException)
            {
                //User aborted renaming
                NewName = Name;
                return false;
            }
        }
    }
}