using System;

namespace imady.Message
{
    public class MadYMessageBase : IDisposable
    {
        public DateTime timeSend { get; set; }

        public Guid senderId { get; set; }

        public string sender { get; set; }


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
            sender = userName;
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
                // TODO：这里只释放了LeeMessage对象，而没有释放T（messageBody）。需要为T类都实现IDisposable。

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

