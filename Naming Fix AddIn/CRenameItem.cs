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

using EnvDTE;
using EnvDTE80;

namespace NamingFix
{
    class CRenameItem
    {
        private string _Name;
        public string Name
        {
            get { return _Name; }
            set
            {
                _Name = value;
                NewName = value;
            }
        }
        public string NewName;
        public CodeElement2 Element;
        public IRenameItemInterface Parent;
        public virtual bool ReadOnly { get; set; }

        protected T GetElement<T>()
        {
            return (T)Element;
        }
    }

    class CRenameItemProperty : CRenameItem
    {
        public CodeVariable2 GetElement()
        {
            return GetElement<CodeVariable2>();
        }
    }

    class CRenameItemVariable : CRenameItemProperty {}

    class CRenameItemParameter : CRenameItemVariable {}

    class CRenameItemType : CRenameItem
    {
        public virtual CRenameItem FindTypeByName(string typeName)
        {
            return Name == typeName ? this : null;
        }
    }

    class CRenameItemEnum : CRenameItemType
    {
        public CodeEnum GetElement()
        {
            return GetElement<CodeEnum>();
        }
    }

    class CRenameItemStruct : CRenameItemType
    {
        public CodeStruct GetElement()
        {
            return GetElement<CodeStruct>();
        }
    }

    class CRenameItemDelegate : CRenameItemType
    {
        public CodeDelegate2 GetElement()
        {
            return GetElement<CodeDelegate2>();
        }
    }
}