using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core.Utils
{
    public static class EventUtil
    {
        public static void Raise(EventHandler handler, object sender)
        {
            if (handler == null) return;

            try
            {
                handler.Invoke(sender, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageService.Exception(ex);
            }
        }
    }
}