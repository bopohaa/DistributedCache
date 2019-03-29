using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DistributedCache.Common.Internal
{
    internal class MemoryStreamReader : Stream
    {
        private int _length;
        private int _position;
        private byte[] _buffer;

        public MemoryStreamReader()
        {

        }

        public void SetBuffer(byte[] data)
        {
            _buffer = data;
            _length = data.Length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => _position; set => _position = (int)value; }

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var cnt = Math.Min(_length - _position, count);
            if (cnt == 0) return 0;
            Array.Copy(_buffer, _position, buffer, offset, cnt);
            _position += cnt;
            return cnt;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = (int)offset;
                    break;
                case SeekOrigin.Current:
                    _position += (int)offset;
                    break;
                case SeekOrigin.End:
                    _position = _length - (int)offset;
                    break;
                default:
                    break;
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            _position = 0;
            _length = (int)value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
