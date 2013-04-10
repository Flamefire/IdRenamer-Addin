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
using Extensibility;
using Microsoft.VisualStudio.CommandBars;
using System;

namespace NamingFix
{
    /// <summary>Das Objekt für die Implementierung eines Add-Ins.</summary>
    /// <seealso class='IDTExtensibility2' />
    // ReSharper disable UnusedMember.Global
    public class CConnect : IDTExtensibility2, IDTCommandTarget, IDisposable
        // ReSharper restore UnusedMember.Global
    {
        private const string _ExecCmdName = "Exec";
        private CNamingFix _Fixer;

        /// <summary>Implementiert die OnConnection-Methode der IDTExtensibility2-Schnittstelle. Empfängt eine Benachrichtigung, wenn das Add-In geladen wird.</summary>
        /// <param name="application">Stammobjekt der Hostanwendung.</param>
        /// <param name="connectMode">Beschreibt, wie das Add-In geladen wird.</param>
        /// <param name="addInInst">Objekt, das dieses Add-In darstellt.</param>
        /// <param name="custom"></param>
        /// <seealso class='IDTExtensibility2' />
        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            _ApplicationObject = (DTE2)application;
            _AddInInstance = (AddIn)addInInst;
            _Fixer = new CNamingFix(_ApplicationObject);
            if (connectMode != ext_ConnectMode.ext_cm_UISetup)
                return;
            object[] contextGuids = new object[] {};
            Commands2 commands = (Commands2)_ApplicationObject.Commands;
            const string toolsMenuName = "Tools";

            //Platzieren Sie den Befehl im Menü "Tools".
            //Suchen Sie die MenuBar-Befehlsleiste, die oberste Befehlsleiste mit allen Hauptmenüelementen:
            CommandBar menuBarCommandBar = ((CommandBars)_ApplicationObject.CommandBars)["MenuBar"];

            //Suchen Sie die Tools-Befehlsleiste in der MenuBar-Befehlsleiste:
            CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
            CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

            //Dieser try/catch-Block kann dupliziert werden, wenn Sie mehrere Befehle hinzufügen möchten, die von dem Add-In verarbeitet werden sollen.
            //  Sie müssen nur sicherstellen, dass Sie auch die QueryStatus/Exec-Methode aktualisieren, um die neuen Befehlsnamen einzuschließen.
            try
            {
                //Fügen Sie der Befehlsauflistung einen Befehl hinzu:
                Command command = commands.AddNamedCommand2(_AddInInstance, _ExecCmdName, "Apply namesheme", "Starts the analysis", true, 0940, ref contextGuids);

                //Fügen Sie dem Menü "Tools" ein Steuerelement für den Befehl hinzu:
                if ((command != null) && (toolsPopup != null))
                    command.AddControl(toolsPopup.CommandBar);
            }
            catch (ArgumentException)
            {
                //An dieser Stelle tritt die Ausnahme wahrscheinlich auf, weil bereits ein Befehl mit diesem Namen
                //  vorhanden ist. Ist dies der Fall, muss der Befehl nicht erneut erstellt werden, und 
                //  die Ausnahme kann sicher ignoriert werden.
            }
        }

        /// <summary>Implementiert die OnDisconnection-Methode der IDTExtensibility2-Schnittstelle. Empfängt eine Benachrichtigung, wenn das Add-In entladen wird.</summary>
        /// <param name='disconnectMode'>Beschreibt, wie das Add-In entladen wird.</param>
        /// <param name='custom'>Array von spezifischen Parametern für die Hostanwendung.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom) {}

        /// <summary>Implementiert die OnAddInsUpdate-Methode der IDTExtensibility2-Schnittstelle. Empfängt eine Benachrichtigung, wenn die Auflistung von Add-Ins geändert wurde.</summary>
        /// <param name='custom'>Array von spezifischen Parametern für die Hostanwendung.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnAddInsUpdate(ref Array custom) {}

        /// <summary>Implementiert die OnStartupComplete-Methode der IDTExtensibility2-Schnittstelle. Empfängt eine Benachrichtigung, wenn der Ladevorgang der Hostanwendung abgeschlossen ist.</summary>
        /// <param name='custom'>Array von spezifischen Parametern für die Hostanwendung.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnStartupComplete(ref Array custom) {}

        /// <summary>Implementiert die OnBeginShutdown-Methode der IDTExtensibility2-Schnittstelle. Empfängt eine Benachrichtigung, wenn die Hostanwendung entladen wird.</summary>
        /// <param name='custom'>Array von spezifischen Parametern für die Hostanwendung.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnBeginShutdown(ref Array custom) {}

        /// <summary>Implementiert die QueryStatus-Methode der IDTCommandTarget-Schnittstelle. Diese wird aufgerufen, wenn die Verfügbarkeit des Befehls aktualisiert wird.</summary>
        /// <param name='commandName'>Der Name des Befehls, dessen Zustand ermittelt werden soll.</param>
        /// <param name='neededText'>Für den Befehl erforderlicher Text.</param>
        /// <param name='status'>Der Zustand des Befehls in der Benutzeroberfläche.</param>
        /// <param name='commandText'>Vom neededText-Parameter angeforderter Text.</param>
        /// <seealso class='Exec' />
        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
            if (neededText != vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
                return;
            if (commandName == GetType().FullName + "." + _ExecCmdName)
                // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
                status = vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
            // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
        }

        /// <summary>Implementiert die Exec-Methode der IDTCommandTarget-Schnittstelle. Diese wird aufgerufen, wenn der Befehl aufgerufen wird.</summary>
        /// <param name='commandName'>Der Name des auszuführenden Befehls.</param>
        /// <param name='executeOption'>Beschreibt, wie der Befehl ausgeführt werden muss.</param>
        /// <param name='varIn'>Parameter, die vom Aufrufer an den Befehlshandler übergeben werden.</param>
        /// <param name='varOut'>Parameter, die vom Befehlshandler an den Aufrufer übergeben werden.</param>
        /// <param name='handled'>Informiert den Aufrufer, ob der Befehl bearbeitet wurde oder nicht.</param>
        /// <seealso class='Exec' />
        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            if (handled)
                return;
            handled = false;
            if (executeOption != vsCommandExecOption.vsCommandExecOptionDoDefault
                || commandName != GetType().FullName + "." + _ExecCmdName)
                return;
            _Fixer.DoFix();
            handled = true;
        }

        private DTE2 _ApplicationObject;
        private AddIn _AddInInstance;

        #region IDisposable Implemetation
        private bool _IsDisposed;

        ~CConnect()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Closes and disposes the <see cref="CConnect" />
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     This method is called by <see cref="Dispose()" />.
        ///     Derived classes can override this method.
        /// </summary>
        /// <param name="cleanManagedResources">
        ///     <para>
        ///         <see langword="true" />: Disposes managed and unmanaged resources
        ///     </para>
        ///     <para>
        ///         <see langword="false" />: Disposes only unmanaged resources
        ///     </para>
        /// </param>
        private void Dispose(bool cleanManagedResources)
        {
            if (!_IsDisposed)
            {
                if (cleanManagedResources)
                {
                    // Clean up the managed resources
                    if (_Fixer != null)
                        _Fixer.Dispose();
                }
                // Clean up the unmanaged resources
            }
            _IsDisposed = true;
        }
        #endregion
    }
}