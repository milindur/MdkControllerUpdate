using System;

namespace MdkControllerUpdate.Model
{
    class Release
    {
        public string Label { get; set; }
        public string Description { get; set; }
        public string FirmwareUri { get; set; }
        public DateTimeOffset CreatedOn { get; set; }
    }
}
