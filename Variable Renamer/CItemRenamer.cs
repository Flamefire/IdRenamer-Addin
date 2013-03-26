using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;

namespace NoCompany.Variable_Renamer
{
    enum ENamingStyle
    {
        UpperCamelCase,
        LowerCamelCase,
        UpperCase,
        LowerCase
    }

    struct SRenameRule
    {
        public bool DontChange;
        public String RemovePrefix;
        public String Prefix;
        public ENamingStyle NamingStyle;
    }

    class CRenameRuleSet
    {
        public const int Priv = 0;
        public const int Prot = 1;
        public const int Pub = 2;

        public SRenameRule
            Parameter,
            LokalVariable,
            LokalConst,
            Interface,
            Class,
            Enum,
            Struct;
        public SRenameRule[]
            Const,
            Field,
            Property,
            Method;

        public CRenameRuleSet()
        {
            Const = new SRenameRule[3];
            Field = new SRenameRule[3];
            Property = new SRenameRule[3];
            Method = new SRenameRule[3];
            Parameter.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalVariable.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalConst.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalVariable.Prefix = "locVar";
            LokalConst.Prefix = "locConst";
            Const[Priv].Prefix = "_";
            Field[Priv].Prefix = "_";
            Method[Priv].Prefix = "_";
            Property[Priv].Prefix = "_";
            Interface.Prefix = "I";
            Class.Prefix = "C";
            Enum.Prefix = "E";
            Struct.Prefix = "S";
        }
    }

    class CItemRenamer
    {
        private const int Priv = 0;
        private const int Prot = 1;
        private const int Pub = 2;
        private readonly DTE2 _Dte;
        private readonly OutputWindowPane _OutputWindow;
        private readonly CRenameRuleSet _RuleSet = new CRenameRuleSet();
        private const string _Id = "[a-z_][a-z0-9_\\.]*";
        private static readonly Regex _ReLocVar = new Regex(@"(?<=[\{\(,;]\s*)((const\s+)?" + _Id + "(<" + _Id + @">)?)\s*(\[\])?\s+(" + _Id + @")(?=\s*([=,;]|( in )))",
                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex _ReString = new Regex(@"""(\\.|[^\\""])*""", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReVarbatimString = new Regex("@\"(\"\"|[^\"])*\"", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _ReCommentSl = new Regex(@"//[^\r\n]*\r\n", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _ReCommentMl = new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _ReUnderScore = new Regex("(?<=[a-z0-9])_[a-z0-9]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _ReMultiCaps = new Regex("[A-Z]{2,}", RegexOptions.Compiled);
        private static readonly Regex _ReEndsWithNoCaps = new Regex("[a-z]$", RegexOptions.Compiled);

        private readonly List<String> _FoundTypes = new List<string>();
        private CTypeResolver _TypeResolver = new CTypeResolver();
        //Strings
        //comments
        public CItemRenamer(DTE2 dte)
        {
            _Dte = dte;
            Window window = _Dte.Windows.Item(Constants.vsWindowKindOutput);
            OutputWindow outputWindow = (OutputWindow)window.Object;
            _OutputWindow = outputWindow.OutputWindowPanes.Add("new pane");
        }

        /// <summary>
        ///     This function is the callback used to execute a command when the a menu item is clicked.
        ///     See the Initialize method to see how the menu item is associated to this function using
        ///     the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        public void MenuItemCallback(object sender, EventArgs e)
        {
            _Dte.UndoContext.Open("Item renaming");
            Execute();
            _Dte.UndoContext.Close();
        }

        private void Execute()
        {
            foreach (Project project in _Dte.Solution.Projects)
            {
                Message(project.Kind);
                IterateProjectItems(project.ProjectItems);
            }
            Message("\r\nFound local var types:");
            foreach (string type in _FoundTypes)
            {
                if (!_TypeResolver.IsType(type))
                    Message(type);
            }
            Message("IMPORTANT: Check if all found types above are actual types otherwhise use undo and fix addin!");
        }

        private void IterateProjectItems(ProjectItems projectItems)
        {
            foreach (ProjectItem item in projectItems)
            {
                if (item.Kind == Constants.vsProjectItemKindPhysicalFile && item.Name.EndsWith(".cs"))
                {
                    Message("File " + item.Name + ":");
                    IterateCodeElements(item.FileCodeModel.CodeElements);
                }
                if (item.SubProject != null)
                    IterateProjectItems(item.SubProject.ProjectItems);
                else
                    IterateProjectItems(item.ProjectItems);
            }
        }

        //Iterate through all the code elements in the provided element
        private void IterateCodeElements(CodeElements colCodeElements)
        {
            if (colCodeElements == null)
                return;
            foreach (CodeElement objCodeElement in colCodeElements)
            {
                try
                {
                    CodeElement2 element = objCodeElement as CodeElement2;
                    switch (element.Kind)
                    {
                        case vsCMElement.vsCMElementVariable:
                            RenameVariable(element);
                            break;
                        case vsCMElement.vsCMElementProperty:
                            RenameProperty(element);
                            break;
                        case vsCMElement.vsCMElementFunction:
                            RenameMethod(element);
                            break;
                        case vsCMElement.vsCMElementParameter:
                            DoRename(element, _RuleSet.Parameter);
                            break;
                        case vsCMElement.vsCMElementInterface:
                            DoRename(element, _RuleSet.Interface);
                            break;
                        case vsCMElement.vsCMElementEnum:
                            DoRename(element, _RuleSet.Enum);
                            break;
                        case vsCMElement.vsCMElementStruct:
                            DoRename(element, _RuleSet.Struct);
                            break;
                        case vsCMElement.vsCMElementNamespace:
                            CodeNamespace objCodeNamespace = objCodeElement as CodeNamespace;
                            IterateCodeElements(objCodeNamespace.Members);
                            break;
                        case vsCMElement.vsCMElementClass:
                            DoRename(element, _RuleSet.Class);
                            CodeClass objCodeClass = objCodeElement as CodeClass;
                            IterateCodeElements(objCodeClass.Members);
                            break;
                    }
                }
                catch {}
            }
        }

        private void RenameVariable(CodeElement2 element)
        {
            CodeVariable2 variable = (CodeVariable2)element;
            if (variable.IsConstant)
                DoRename(element, _RuleSet.Const, variable.Access);
            else
                DoRename(element, _RuleSet.Field, variable.Access);
        }

        private void RenameProperty(CodeElement2 element)
        {
            CodeProperty2 func = (CodeProperty2)element;
            DoRename(element, _RuleSet.Property, func.Access);
        }

        private void RenameMethod(CodeElement2 element)
        {
            CodeFunction2 func = (CodeFunction2)element;
            DoRename(element, _RuleSet.Method, func.Access);
            EditPoint startPoint = func.GetStartPoint(vsCMPart.vsCMPartBody).CreateEditPoint();
            TextPoint endPoint = func.GetEndPoint(vsCMPart.vsCMPartBody);
            string funcText = "{" + startPoint.GetText(endPoint);

            #region Remove all strings and comments
            List<string> strings = new List<string>();
            List<string> comments = new List<string>();
            funcText = _ReString.Replace(funcText, delegate(Match m)
                {
                    strings.Add(m.Value);
                    return "\"ReplacedStr:::" + (strings.Count - 1) + ":::\"";
                });
            funcText = _ReVarbatimString.Replace(funcText, delegate(Match m)
                {
                    strings.Add(m.Value);
                    return "\"ReplacedStr:::" + (strings.Count - 1) + ":::\"";
                });
            funcText = _ReCommentSl.Replace(funcText, delegate(Match m)
                {
                    comments.Add(m.Value);
                    return "//ReplacedCom:::" + (comments.Count - 1) + ":::;\r\n";
                });
            funcText = _ReCommentMl.Replace(funcText, delegate(Match m)
                {
                    comments.Add(m.Value);
                    return "//ReplacedCom:::" + (comments.Count - 1) + ":::;\r\n";
                });
            #endregion

            MatchCollection locVars = _ReLocVar.Matches(funcText);
            if (locVars.Count > 0)
            {
                Message("Method " + element.Name + ":");
                //First capture all vars, rename, check for 2 vars with same name and possible colliding space
                foreach (Match match in locVars)
                {
                    string type = match.Groups[1].Value.ToLower();
                    string name = match.Groups[5].Value;
                    if (type == "return" || type == "else" || type == "in" || type == "out" || type == "ref")
                        continue;
                    type = match.Groups[1].Value;
                    if (!_FoundTypes.Contains(type))
                        _FoundTypes.Add(type);
                    Message(name + "\t(" + type + ")");
                    string newName;
                    if (match.Groups[2].Value != "")
                        newName = DoRename(name, _RuleSet.LokalConst);
                    else
                        newName = DoRename(name, _RuleSet.LokalVariable);
                    if (name != newName)
                        funcText = Regex.Replace(funcText, @"(?<! new )(?<!\w|\.)" + Regex.Escape(name) + @"(?=( in )|\b(?!\s+[a-zA-Z_]))", newName, RegexOptions.Singleline);
                }
                //Restore strings and comments
                for (int i = 0; i < strings.Count; i++)
                    funcText = funcText.Replace("\"ReplacedStr:::" + i + ":::\"", strings[i]);
                for (int i = 0; i < comments.Count; i++)
                    funcText = funcText.Replace("//ReplacedCom:::" + i + ":::;\r\n", comments[i]);

                startPoint.ReplaceText(endPoint, funcText.Substring(1), 0);
            }
            IterateCodeElements(func.Parameters);
        }

        private static void DoRename(CodeElement2 element, SRenameRule[] rules, vsCMAccess access)
        {
            switch (access)
            {
                case vsCMAccess.vsCMAccessPrivate:
                    DoRename(element, rules[Priv]);
                    break;
                case vsCMAccess.vsCMAccessProtected:
                    DoRename(element, rules[Prot]);
                    break;
                case vsCMAccess.vsCMAccessPublic:
                    DoRename(element, rules[Pub]);
                    break;
            }
        }

        private static void DoRename(CodeElement2 element, SRenameRule rule)
        {
            string newName = DoRename(element.Name, rule);

            //if (!element.Name.Equals(newName))
            //element.RenameSymbol(newName);
        }

        private static string DoRename(string theString, SRenameRule rule)
        {
            if (rule.DontChange)
                return theString;

            RemovePrefix(ref theString, rule.RemovePrefix);
            RemovePrefix(ref theString, rule.Prefix);

            switch (rule.NamingStyle)
            {
                case
                    ENamingStyle.LowerCamelCase:
                    if (rule.Prefix != null && _ReEndsWithNoCaps.IsMatch(rule.Prefix))
                        ToUpperCamelCase(ref theString);
                    else
                        ToLowerCamelCase(ref theString);
                    break;
                case ENamingStyle.UpperCamelCase:
                    ToUpperCamelCase(ref theString);
                    break;
                case ENamingStyle.UpperCase:
                    theString = theString.ToUpper();
                    break;
                case ENamingStyle.LowerCase:
                    theString = theString.ToLower();
                    break;
            }
            AddPrefix(ref theString, rule.Prefix);

            return theString;
        }

        private static void RemovePrefix(ref string theString, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return;
            while (theString.StartsWith(prefix))
            {
                theString = theString.Remove(0, prefix.Length);
                if (prefix.Length == 1)
                    break;
            }
        }

        private static void ToLowerCamelCase(ref String theString)
        {
            ToCamelCase(ref theString);
            theString = theString.Substring(0, 1).ToLower() + theString.Substring(1);
        }

        private static void ToUpperCamelCase(ref String theString)
        {
            ToCamelCase(ref theString);
            theString = theString.Substring(0, 1).ToUpper() + theString.Substring(1);
        }

        private static void ToCamelCase(ref String theString)
        {
            theString = theString.Replace("__", "_");
            theString = _ReMultiCaps.Replace(theString, m => m.Value[0] + m.Value.Substring(1).ToLower());
            theString = _ReUnderScore.Replace(theString, m => m.Value[1].ToString().ToUpper());
        }

        private static void AddPrefix(ref String theString, string prefix = "")
        {
            if (String.IsNullOrEmpty(prefix))
                return;
            if (!theString.StartsWith(prefix))
                theString = prefix + theString;
        }

        private void Message(string msg)
        {
            _OutputWindow.Activate();
            _OutputWindow.OutputString(msg + "\r\n");
        }
    }

    class CTypeResolver
    {
        private readonly Dictionary<string, Type> _Alias = new Dictionary<string, Type>();

        public CTypeResolver()
        {
            _Alias.Add("bool", typeof(bool));
            _Alias.Add("byte", typeof(byte));
            _Alias.Add("sbyte", typeof(sbyte));
            _Alias.Add("char", typeof(char));
            _Alias.Add("decimal", typeof(decimal));
            _Alias.Add("double", typeof(double));
            _Alias.Add("float", typeof(float));
            _Alias.Add("int", typeof(int));
            _Alias.Add("uint", typeof(uint));
            _Alias.Add("long", typeof(long));
            _Alias.Add("ulong", typeof(ulong));
            _Alias.Add("object", typeof(object));
            _Alias.Add("short", typeof(short));
            _Alias.Add("ushort", typeof(ushort));
            _Alias.Add("string", typeof(string));
        }

        public bool IsType(string typeName)
        {
            if (_Alias.ContainsKey(typeName.ToLower()))
                return true;
            return Type.GetType(typeName, false, true) != null;
        }
    }
}