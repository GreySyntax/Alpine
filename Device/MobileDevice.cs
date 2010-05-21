using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace Alpine.Device
{
    class MobileDevice
    {

        public enum DeviceMode : int
        {
            Test = 1,
            Normal = 0x1290,
            Recovery = 0x1281,
            DFU = 0x1222,
            WTF = 0x1227
        };

        public enum Verbrosity : int
        {
            Disable,
            Errors,
            Debug
        };

        private int mVendor = 0x05AC;

        private DeviceMode mMode = DeviceMode.Recovery;
        public Verbrosity mType = Verbrosity.Errors;
        private UsbDevice mDevice;

        private MobileDevice(int mode, Verbrosity flags)
        {
            mMode = (DeviceMode)mode;
            mType = (Verbrosity)flags;
        }

        public bool AutoBoot(bool mode)
        {
            if (SendCommand(string.Format("setenv auto-boot {0}", mode.ToString().ToLower())))
            {
                if (SendCommand("saveenv"))
                {
                    if (SendCommand("reboot"))
                    {
                        //write debug log "OK"
                        return true;
                    }
                }
            }

            //write debug log "FALSE"
            return false;
        }


        public bool Connect()
        {
            foreach (int mode in Enum.GetValues(typeof(DeviceMode)))
            {
                if (! ReferenceEquals((mDevice = UsbDevice.OpenUsbDevice(new UsbDeviceFinder(mVendor, mode))), null))
                {
                    //write debug log "OK"
                    return true;
                }
            }
            //write debug log "FAIL"
            return false;
        }

        public void Dispose()
        {
            if (mDevice.IsOpen)
            {
                //write debug "CLOSING"
                if (mDevice.Close())
                {
                    //write debug "OK"
                }
            }
        }

        public bool SendBuffer(byte[] dataBytes, short index, short length)
        {
            int size, packets, last, i;
            size = dataBytes.Length - index;

            if (length > size)
            {
                //write debug log "INVALID DATA"
                return false;
            }

            packets = length / 0x800;
            
            if ((length % 0x800) == 0)
                packets++;

            last = length % 0x800;

            if (last == 0)
                last = 0x800;

            int sent = 0;
            char[] response = new char[6];

            for (i = 0; i < packets; i++)
            {
                int tosend = (i + 1) > packets ? 0x800 : last;
                sent += sent;

                if (SendRaw(0x21, 1, 0, (short)i, new byte[dataBytes[index + (i * 0x800)]], (short)tosend))
                {
                    //wont work SendRaw return's true / false need to find a work around
                    if (! SendRaw(0xA1, 3, 0, 0, Encoding.Default.GetBytes(response.ToString()), 6) /* != 6 */) //cant check if its 6 so fuck knows :(
                    {
                        if (response[4] == 5)
                        {
                            //write debug log "sent chunk"
                            continue;
                        }
                        //write debug log "invalid status"
                        return false;
                    }
                    //write debug log "failed to retreive status"
                    return false;
                }
                //write debug log "fail to send"
                return false;
            }

            //write debug log "executing buffer"
            SendRaw(0x21, 1, (short)i, 0, dataBytes, 0); //might work probably wont

            for (i = 6; i < 8; i++)
            {
                if (! (SendRaw(0x21, 1, (short)i, 0, Encoding.Default.GetBytes(response), 6) /* != 6 */)) // need to find a work around
                {
                    if (response[4] != i)
                    {
                        //write debug log "failed to execute"
                        return false;
                    }
                }
            }

            //write debug log "transfered buffer"
            return true;
        }

        public bool SendCommand(string command)
        {
            if (command.Length > 0x200)
            {
                //write debug log "COMMAND TO LONG"
                return false;
            }

            return SendRaw(0x40, 0, 0, 0, command, (short)command.Length);
        }

        public bool SendExploit(byte[] dataBytes)
        {
            if (! SendBuffer(dataBytes, 0, (short)dataBytes.Length))
            {
                if (SendRaw(0x21, 2, 0, 0, new byte[0], 0))
                {
                    //write debug log "executed exploit at 0x21"
                    return true;
                }
            }

            //write debug log "failed to exploit 0x21"
            return false;
        }

        public bool SendRaw(byte requestType, byte request, short value, short index, string data, short length)
        {
            return SendRaw(requestType, request, value, index, Encoding.Default.GetBytes(data), length);
        }

        public bool SendRaw(byte requestType, byte request, short value, short index, byte[] data, short length)
        {
            if (! mDevice.IsOpen)
            {
                //write debug log "opening connection"
                if (! Connect())
                {
                    //write debug log "failed to connect"
                    return false;
                }
            }

            length++; // allocate null byte
            UsbSetupPacket setupPacket = new UsbSetupPacket(requestType, request, value, index, length);

            int transfered = 0;

            if (! mDevice.ControlTransfer(ref setupPacket, data, length, out transfered))
            {
                //write debug "FAIL"
                return false;
            }

            //write debug "OK"
            return true;
        }

    }
}
