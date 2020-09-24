using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

//Logger from https://stackoverflow.com/a/17259945
namespace Log
{
    public interface ILogger
    {
        void WriteLine(string msg);
    }

    internal class Param
    {
        internal enum LogType { Info, Warning, Error, SimpleError };

        internal LogType Ltype { get; set; }  // Type of log
        internal string Msg { get; set; }     // Message
        internal string Action { get; set; }  // Action when error or warning occurs (optional)
        internal string Obj { get; set; }     // Object that was processed whend error or warning occurs (optional)

        internal Param()
        {
            Ltype = LogType.Info;
            Msg = "";
        }
        internal Param(LogType logType, string logMsg)
        {
            Ltype = logType;
            Msg = logMsg;
        }
        internal Param(LogType logType, string logMsg, string logAction, string logObj)
        {
            Ltype = logType;
            Msg = logMsg;
            Action = logAction;
            Obj = logObj;
        }
    }

    // Reentrant Logger written with Producer/Consumer pattern.
    // It creates a thread that receives write commands through a Queue (a BlockingCollection).
    // The user of this log has just to call Logger.WriteLine() and the log is transparently written asynchronously.

    public class Logger : ILogger
    {
        private string file;
        private TextWriter filewritter;
        BlockingCollection<Param> bc = new BlockingCollection<Param>();

        // Constructor create the thread that wait for work on .GetConsumingEnumerable()
        public Logger(string filename)
        {
            file = filename;
            filewritter = new StreamWriter(file);
            Console.WriteLine("Results will be saved at {0}", file);

            Task.Factory.StartNew(() =>
            {
                foreach (Param p in bc.GetConsumingEnumerable())
                {
                    filewritter.WriteLine(p.Msg);
                }
            });
        }

        ~Logger()
        {
            // Free the writing thread
            bc.CompleteAdding();

        }

        public void Close()
        {
            filewritter.Close();
        }

        // Just call this method to log something (it will return quickly because it just queue the work with bc.Add(p))
        public void WriteLine(string msg)
        {
            Param p = new Param(Param.LogType.Info, msg);
            bc.Add(p);
        }

        internal void Write(string v)
        {
            throw new NotImplementedException();
        }
    }
}