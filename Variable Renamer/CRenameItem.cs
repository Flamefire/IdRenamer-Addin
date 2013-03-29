#region license
/*
    This file is part of the item renamer Add-In for VS ("Add-In").

    The Add-In is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    The Add-In is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
 */
#endregion

using EnvDTE;
using EnvDTE80;

namespace Variable_Renamer
{
    class CRenameItem
    {
        public string Name, NewName;
        public CodeElement2 Element;
        public IRenameItemInterface Parent;

        protected T GetElement<T>()
        {
            return (T)Element;
        }
    }

    class CRenameItemVariable : CRenameItem
    {
        public CodeVariable2 GetElement()
        {
            return GetElement<CodeVariable2>();
        }
    }

    class CRenameItemType : CRenameItem
    {
        public virtual CRenameItem FindTypeName(string typeName)
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
}