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
            return (IsSystem || Element==null)?null:CUtils.GetCodeElementAtTextPoint(_StartPoint, _Kind, _ProjectItem);
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