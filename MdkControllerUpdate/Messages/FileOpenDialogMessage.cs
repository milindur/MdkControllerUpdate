using System;
using GalaSoft.MvvmLight.Messaging;

namespace MdkControllerUpdate.Messages
{
    class FileOpenDialogMessage : MessageBase
    {
        public FileOpenDialogMessage(Action<string> fileSelected, Action canceled)
        {
            FileSelected = fileSelected;
            Canceled = canceled;
        }

        public Action<string> FileSelected { get; }
        public Action Canceled { get; }
    }
}
