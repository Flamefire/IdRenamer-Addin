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

using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using System.Collections.Generic;
using System.Linq;

namespace NamingFix
{
    abstract class CRenameItem
    {
        public virtual bool IsSystem { get; set; }
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
        public IRenameItemContainer Parent { get; set; }
        public abstract ProjectItem ProjectItem { get; }
        public abstract TextPoint StartPoint { get; }

        public abstract void Rename();
        private static readonly Regex _ReCaps = new Regex("(?<=[a-z])[A-Z]", RegexOptions.Compiled);

        /// <summary>
        /// Gets one element whose name is conflicting with this one
        /// Or null if no conflict is found 
        /// </summary>
        /// <returns></returns>
        public abstract CRenameItem GetConflictItem();

        public string GetTypeName()
        {
            string typeName = GetType().Name.Substring("CRenameItem".Length);
            return _ReCaps.Replace(typeName, m => " " + m.Value.ToLower());
        }

        public void Show()
        {
            if (ProjectItem.Document == null || ProjectItem.Document.Windows.Count == 0)
                ProjectItem.Open(Constants.vsViewKindCode);
            StartPoint.TryToShow(vsPaneShowHow.vsPaneShowTop);
        }
    }

    abstract class CRenameItemElement : CRenameItem
    {
        protected CodeElement InternalElement;
        public virtual CodeElement Element
        {
            private get { return InternalElement; }
            set
            {
                InternalElement = value;
                Name = value.Name;
            }
        }

        protected T GetElement<T>()
        {
            return (T)Element;
        }

        public override ProjectItem ProjectItem
        {
            get { return InternalElement.ProjectItem; }
        }
        public override TextPoint StartPoint
        {
            get { return InternalElement.StartPoint; }
        }

        public override void Rename()
        {
            if (NewName == Name)
                return;
            CodeElement2 element2 = Element as CodeElement2;
            if (element2 == null)
                return;
            element2.RenameSymbol(NewName);
            Name = NewName;
        }
    }

    interface IRenameItemContainer
    {
        string Name { get; }
        void Add(CRenameItem item);

        /// <summary>
        ///     Checks if given Id collides with local variable
        /// </summary>
        CRenameItem GetConflictLocVar(string newName, string oldName);

        /// <summary>
        ///     Checks if given Id collides with type
        /// </summary>
        CRenameItem GetConflictType(string newName, string oldName);

        /// <summary>
        ///     Checks if given Id collides with Id(Property, Variable, Function)
        /// </summary>
        CRenameItem GetConflictId(string newName, string oldName);

        /// <summary>
        ///     Finds given typename, which is valid in context of current class
        /// </summary>
        CRenameItem FindTypeByName(string typeName);
    }

    class CRenameItemList<T> : List<T> where T : CRenameItem
    {
        public void Add(CRenameItem item)
        {
            base.Add((T)item);
        }

        public CRenameItem GetConflict(string newName, string oldName)
        {
            return this.FirstOrDefault(item => item.NewName == newName && item.Name != oldName);
        }

        public T Find(string name)
        {
            return this.FirstOrDefault(item => item.Name == name);
        }
    }

    abstract class CRenameItemVariableBase : CRenameItemElement
    {
        public override CRenameItem GetConflictItem()
        {
            if (Name == NewName)
                return null;
            CRenameItem item = Parent.GetConflictLocVar(NewName, Name);
            return item ?? Parent.GetConflictId(NewName, Name);
        }
    }

    class CRenameItemParameter : CRenameItemVariableBase
    {
        public CodeVariable2 GetElement()
        {
            return GetElement<CodeVariable2>();
        }
    }

    class CRenameItemProperty : CRenameItemVariableBase
    {
        public CodeProperty2 GetElement()
        {
            return GetElement<CodeProperty2>();
        }
    }

    class CRenameItemVariable : CRenameItemParameter {}

    class CRenameItemType : CRenameItemElement
    {
        public override CRenameItem GetConflictItem()
        {
            if (Name == NewName)
                return null;
            return Parent.GetConflictType(NewName, Name);
        }
    }

    class CRenameItemEvent : CRenameItemVariableBase
    {
        public CodeEvent GetElement()
        {
            return GetElement<CodeEvent>();
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