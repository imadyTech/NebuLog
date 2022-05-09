using System;

namespace imady.Message
{
    public class MadYMessageBase : IDisposable
    {
        public DateTime timeSend { get; set; }

        public Guid senderId { get; set; }

        public string senderName { get; set; }


        public MadYMessageBase()
        {
            timeSend = DateTime.Now;
        }
        public MadYMessageBase(Guid userId) : this()
        {
            senderId = userId;
        }
        public MadYMessageBase(string userName) : this()
        {
            senderName = userName;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    // Release managed resources
                }
                // Release unmanaged resources
                // TODO������ֻ�ͷ���LeeMessage���󣬶�û���ͷ�T��messageBody������ҪΪT�඼ʵ��IDisposable��

                m_disposed = true;
            }
        }

        ~MadYMessageBase()
        {
            Dispose(false);
        }

        private bool m_disposed;
    }
}

