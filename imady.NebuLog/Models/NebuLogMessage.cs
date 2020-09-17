using System;
using System.Collections.Generic;
using System.Text;

namespace imady.NebuLog
{
    public class NebuLogMessage
    {
        /// <summary>
        /// 日志等级
        /// </summary>
        public string LogLevel { get; set; }
        /// <summary>
        /// 项目名
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// 发送信息的主体
        /// </summary>
        public string SenderName { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        public string LoggingMessage { get; set; }

        /// <summary>
        /// 客户端发出信息的时间（非服务端收到的时间）
        /// </summary>
        public DateTime TimeOfLog { get; set; }
    }

    #region 实现INotifyPropertyChanged的代码（但是破坏了model的POCO状态因此不用）
    /*
    public class NebuLogMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        string _LogLevel;
        /// <summary>
        /// 日志等级
        /// </summary>
        public string LogLevel
        {
            get { return _LogLevel; }
            set
            {
                _LogLevel = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("_LogLevel"));
                }
            }
        }


        string _projectName;
        /// <summary>
        /// 项目名
        /// </summary>
        public string ProjectName
        {
            get { return _projectName; }
            set
            {
                _projectName = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("ProjectName"));
                }
            }
        }


        string _SenderName;
        /// <summary>
        /// 发送信息的主体
        /// </summary>
        public string SenderName
        {
            get { return _SenderName; }
            set
            {
                _SenderName = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("SenderName"));
                }
            }
        }


        string _LoggingMessage;
        /// <summary>
        /// 消息内容
        /// </summary>
        public string LoggingMessage
        {
            get { return _LoggingMessage; }
            set
            {
                _LoggingMessage = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("LoggingMessage"));
                }
            }
        }

        DateTime _timeOfLog;
        /// <summary>
        /// 客户端发出信息的时间（非服务端收到的时间）
        /// </summary>
        public DateTime TimeOfLog
        {
            get { return _timeOfLog; }
            set
            {
                _timeOfLog = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("TimeOfLog"));
                }
            }
        }
    }
    */
    #endregion
}
