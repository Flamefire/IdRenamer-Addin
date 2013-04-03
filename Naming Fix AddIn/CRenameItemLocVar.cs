using System.Text.RegularExpressions;
using EnvDTE;

namespace NamingFix
{
    class CRenameItemLocVar : CRenameItem
    {
        public bool IsConst;
        public new CRenameItemMethod Parent
        {
            private get { return (CRenameItemMethod)base.Parent; }
            set { base.Parent = value; }
        }
        public override ProjectItem ProjectItem
        {
            get { return Parent.ProjectItem; }
        }
        public override TextPoint StartPoint
        {
            get { return Parent.StartPoint; }
        }
        public override TextPoint EndPoint
        {
            get { return Parent.EndPoint; }
        }

        public override void Rename()
        {
            if (Name == NewName)
                return;
            CRenameItemMethod parent = Parent;
            string nameRe = Regex.Escape(Name);
            if (nameRe[0] == '@')
                nameRe = "@?" + nameRe.Substring(1);
            parent.Text = Regex.Replace(parent.Text, @"(?<! new )(?<!\w|\.)" + nameRe + @"(?=( in )|\b(?!\s+[a-zA-Z_]))", NewName, RegexOptions.Singleline);
        }

        public override bool IsRenameValid()
        {
            if (Name == NewName)
                return true;
            return !Parent.IsConflictLocVar(NewName, Name) && !Parent.IsConflictId(NewName, Name);
        }
    }
}