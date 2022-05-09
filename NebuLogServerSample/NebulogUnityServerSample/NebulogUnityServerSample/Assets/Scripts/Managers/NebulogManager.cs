using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using imady.Event;
using imady.Message;
using imady.NebuLog;


namespace NebulogUnityServer
{
    public class NebulogManager : MadYEventObjectBase
    {
        private List<NebuLogMessageRequest> _messageList;
        public List<NebuLogMessageRequest> messageList
        {
            get { if (_messageList == null) _messageList = new List<NebuLogMessageRequest>(); return _messageList; }
            set => _messageList = value;
        }

        private List<NebuLogAddStatRequest> _statList;
        private long _messageCount;
        public List<NebuLogAddStatRequest> statList
        {
            get { if (_statList == null) _statList = new List<NebuLogAddStatRequest>(); return _statList; }
            set => _statList = value;
        }


        #region 响应来自NebuLogHub的事件，进行前端视图的处理
        public void OnLoggingMessageReceived(object sender, NebuLogMessageRequest request)
        {
            if (request == null) return;

            //try
            //{
            //    messageList.Add(request);

            //    this.Dispatcher.Invoke(() =>
            //    {
            //        MessageData.Items.Add(request);
            //        MessageData.ScrollIntoView(request);//注意：AutoScroll会导致客户端渲染速度大幅下降
            //        _messageCount++;
            //        TestMessageBox.Text = $"Total received {_messageCount} messages.";

            //    }
            //    //MessageData.Add(new DataGridTextColumn {  })
            //    );
            //}
            //catch (Exception ex)
            //{
            //    messageList.Add(new NebuLogMessageRequest()
            //    {
            //        LogLevel = "Server",
            //        LoggingMessage = ex.Message,
            //        ProjectName = Application.Current.MainWindow.Name,
            //        SenderName = Assembly.GetExecutingAssembly().GetName().Name,
            //        TimeOfLog = DateTime.Now
            //    });
            //    //this.Dispatcher.Invoke(()=> TestMessageBox.Text = ex.Message);
            //}
        }
        public void OnAddStatRequestReceived(object sender, NebuLogAddStatRequest request)
        {
            if (request == null) return;
            //try
            //{
            //    statList.Add(request);

            //    this.Dispatcher.Invoke(() =>
            //    {
            //        StatDataGrid.Items.Add(request);
            //        //StatDataGrid.ScrollIntoView(log);//注意：AutoScroll会导致客户端渲染速度大幅下降
            //        _messageCount++;
            //        TestMessageBox.Text = $"Total received {_messageCount} messages.";

            //    }
            //    //MessageData.Add(new DataGridTextColumn {  })
            //    );
            //}
            //catch (Exception ex)
            //{
            //    messageList.Add(new NebuLogMessageRequest()
            //    {
            //        LogLevel = "Server",
            //        LoggingMessage = ex.Message,
            //        ProjectName = Application.Current.MainWindow.Name,
            //        SenderName = Assembly.GetExecutingAssembly().GetName().Name,
            //        TimeOfLog = DateTime.Now
            //    });
            //    //this.Dispatcher.Invoke(()=> TestMessageBox.Text = ex.Message);
            //}

        }

        public void OnRefreshStatRequestRecieved(object sender, NebuLogRefreshStatRequest request)
        {
            if (request == null) return;
            //try
            //{
            //    var item = statList.Find(stat => stat.StatId.Equals(request.StatId));

            //    this.Dispatcher.Invoke(() =>
            //    {
            //        item.StatValue = request.StatValue;
            //        StatDataGrid.Items.Refresh();
            //        _messageCount++;
            //        TestMessageBox.Text = $"Total received {_messageCount} messages.";

            //    }
            //    //MessageData.Add(new DataGridTextColumn {  })
            //    );
            //}
            //catch (Exception ex)
            //{
            //    messageList.Add(new NebuLogMessageRequest()
            //    {
            //        LogLevel = "Server",
            //        LoggingMessage = ex.Message,
            //        ProjectName = Application.Current.MainWindow.Name,
            //        SenderName = Assembly.GetExecutingAssembly().GetName().Name,
            //        TimeOfLog = DateTime.Now
            //    });
            //    //this.Dispatcher.Invoke(()=> TestMessageBox.Text = ex.Message);
            //}

        }
        #endregion

    }
}
