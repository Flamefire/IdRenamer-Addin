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
        private string _FullName;

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
                _FullName = value.FullName;
            }
        }

        public CodeElement2 GetFreshCodeElement()
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

        public override bool Rename()
        {
            if (NewName == Name)
                return true;
            CodeElement2 element = GetFreshCodeElement();
            if (element.FullName == _FullName)
            {
                if (Element.Name == NewName)
                {
                    Name = NewName;
                    return true;
                }
            }
            else
            {
                CNamingFix.Message("!!!Wrong names: " + element.FullName + "!=" + _FullName);
                try
                {
                    if (Element.Name == NewName)
                    {
                        Name = NewName;
                        return true;
                    }
                }
                catch (COMException)
                {
                    //assume the element has already been changed
                    Name = NewName;
                    return true;
                }
                element = Element as CodeElement2;
            }

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