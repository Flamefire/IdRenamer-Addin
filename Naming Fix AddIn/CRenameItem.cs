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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NamingFix
{
    abstract class CRenameItem
    {
        public virtual bool IsSystem { get; set; }
        private string _Name;
        public virtual string Name
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
        private static readonly Regex _ReCaps = new Regex("(?<=[a-z])[A-Z]", RegexOptions.Compiled);

        public abstract bool Rename();

        /// <summary>
        /// Gets one element whose name is conflicting with this one
        /// Or null if no conflict is found 
        /// </summary>
        /// <param name="swapCheck"></param>
        /// <returns></returns>
        public abstract CRenameItem GetConflictItem(bool swapCheck);

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

        public virtual bool IsRenamingAllowed()
        {
            return !IsSystem;
        }
    }

    interface IRenameItemContainer
    {
        string Name { get; }
        bool IsSystem { get; }
        void Add(CRenameItem item);

        /// <summary>
        ///     Checks if given Id collides with local variable
        /// </summary>
        CRenameItem GetConflictLocVar(string newName, string oldName, bool swapCheck);

        /// <summary>
        ///     Checks if given Id collides with type
        /// </summary>
        CRenameItem GetConflictType(string newName, string oldName, bool swapCheck);

        /// <summary>
        ///     Checks if given Id collides with Id(Property, Variable, Function)
        /// </summary>
        CRenameItem GetConflictId(string newName, string oldName, bool swapCheck);

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

        public CRenameItem GetConflict(string newName, string oldName, bool swapCheck)
        {
            if (swapCheck)
            {
                //Check for an existing item which name is the newName but it should be renamed to something else
                //-->possible conflict for e.g.:
                //class x{public int _X;private int X;} <-- needs to be swapped but without this check you'd have 2 values with same name
                return this.FirstOrDefault(item => item.Name == newName && item.NewName != newName);
            }
            return this.FirstOrDefault(item => item.NewName == newName && item.Name != oldName);
        }

        public T Find(string name)
        {
            return this.FirstOrDefault(item => item.Name == name);
        }
    }

    abstract class CRenameItemVariableBase : CRenameItemElement
    {
        public override CRenameItem GetConflictItem(bool swapCheck)
        {
            if (Name == NewName)
                return null;
            CRenameItem item = Parent.GetConflictLocVar(NewName, Name, swapCheck);
            return item ?? Parent.GetConflictId(NewName, Name, swapCheck);
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

        public override bool IsRenamingAllowed()
        {
            return base.IsRenamingAllowed() &&
                   Name != "this";
        }
    }

    class CRenameItemVariable : CRenameItemParameter {}

    class CRenameItemType : CRenameItemElement
    {
        public override CRenameItem GetConflictItem(bool swapCheck)
        {
            return Name == NewName ? null : Parent.GetConflictType(NewName, Name, swapCheck);
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