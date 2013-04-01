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

using System.Collections.Generic;
using System.Linq;
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
        public IRenameItemContainer Parent;
        public virtual bool ReadOnly { get; set; }

        protected T GetElement<T>()
        {
            return (T)Element;
        }
    }

    interface IRenameItemContainer
    {
        void Add(CRenameItem item);

        /// <summary>
        ///     Checks if given Id collides with Member (Var/Property/Function) of this class and therefore is a invalid name
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="oldName"></param>
        /// <returns></returns>
        bool IsMemberRenameValid(string newName, string oldName);

        /// <summary>
        ///     Checks if given Id collides with any other Id (Var/Property/Function) of this class and therefore is a invalide type name
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="oldName"></param>
        /// <returns></returns>
        bool IsIdRenameValid(string newName, string oldName);
    }

    class CRenameItemList<T> : List<T> where T : CRenameItem
    {
        public void Add(CRenameItem item)
        {
            base.Add((T)item);
        }

        public bool IsRenameValid(string newName, string oldName)
        {
            return this.All(item => item.NewName != newName || item.Name == oldName);
        }

        public T Find(string name)
        {
            return this.FirstOrDefault(item => item.Name == name);
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