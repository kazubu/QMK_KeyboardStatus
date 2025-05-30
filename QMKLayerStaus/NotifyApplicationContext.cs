﻿using QMKLayerStaus.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Management;

namespace QMKLayerStaus
{
    using HidLibrary;
    using System.Windows.Forms;
    class NotifyApplicationContext : ApplicationContext
    {
        readonly NotifyIcon notifyIconLayer = new NotifyIcon();

        private List<HidDevice> _devices = new List<HidDevice>();
        bool _monitorDevice = true;
        HidDevice _ActiveDevice;
        readonly MenuItem miExit = new MenuItem();
        readonly MenuItem miMonitor = new MenuItem();
        readonly MenuItem miDevices = new MenuItem();

        readonly CursorManager cm = new CursorManager();

        private readonly List<ManagementEventWatcher> _watchers = new List<ManagementEventWatcher>();

        public NotifyApplicationContext()
        {
            miExit.Index = 0;
            miExit.Text = "Close";
            miExit.Click += MiExit_Click;
            miDevices.Index = 1;
            miDevices.Text = "Available Devices";

            miMonitor.Index = 2;
            miMonitor.Checked = true;
            miMonitor.Text = "Monitor Keyboard";
            miMonitor.Click += MiMonitor_Click;
  
            notifyIconLayer.Icon = Resources.disconnected;
            notifyIconLayer.Text = "No device detected";
            notifyIconLayer.Visible = true;
            notifyIconLayer.ContextMenu = new ContextMenu();

            notifyIconLayer.ContextMenu.MenuItems.Add(miExit);
            notifyIconLayer.ContextMenu.MenuItems.Add("-");
            notifyIconLayer.ContextMenu.MenuItems.Add(miDevices);
            notifyIconLayer.ContextMenu.MenuItems.Add("-");
            notifyIconLayer.ContextMenu.MenuItems.Add(miMonitor);
            UpdateHidDevices(false);
            StartListeningForDeviceEvents();
        }

        private void MiExit_Click(object sender, EventArgs e)
        {
            notifyIconLayer.Visible = false;
            Dispose(true);
            ExitThread();
        }

        private void MiMonitor_Click(object sender, EventArgs e)
        {
            miMonitor.Checked = !miMonitor.Checked;
            _monitorDevice = miMonitor.Checked;
            if (!_monitorDevice)
            {
                notifyIconLayer.Icon = Resources.disconnected;
                notifyIconLayer.Text = "No device detected";
            }
            else
            {
                foreach (var device in _devices)
                {
                    device.ReadReport(OnReport);
                }
                UpdateHidDevices(false);
            }
        }

        private void StartListeningForDeviceEvents()
        {
            StartManagementEventWatcher("__InstanceCreationEvent");
            StartManagementEventWatcher("__InstanceDeletionEvent");
        }

        private void StartManagementEventWatcher(string eventType)
        {
            
            var watcher = new ManagementEventWatcher($"SELECT * FROM {eventType} WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            watcher.EventArrived += DeviceEvent;
            watcher.Start();
            _watchers.Add(watcher);
        }
        private void CleanupWatchers()
        {
            foreach (var w in _watchers)
            {
                w.EventArrived -= DeviceEvent;
                try { w.Stop(); } catch { }
                w.Dispose();
            }
            _watchers.Clear();
        }

        protected override void OnMainFormClosed(object sender, EventArgs e)
        {
            Dispose(true);
            base.OnMainFormClosed(sender, e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach(var device in _devices)
                {
                    if (device == null)
                        continue;
                    device.MonitorDeviceEvents = false;
                    device.ReadReport(null);
                    device.CloseDevice();
                }

                cm.Dispose();

                CleanupWatchers();
                notifyIconLayer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void DeviceEvent(object sender, EventArrivedEventArgs e)
        {
            (sender as ManagementEventWatcher)?.Stop();

            if (e.NewEvent["TargetInstance"] is ManagementBaseObject)
            {
                var deviceDisconnected = e.NewEvent.ClassPath.ClassName.Equals("__InstanceDeletionEvent");

                UpdateHidDevices(deviceDisconnected);

                (sender as ManagementEventWatcher)?.Start();
            }
        }

        private void UpdateHidDevices(bool disconnected)
        {
            if (_monitorDevice)
            {
                var devices = GetListableDevices().ToList();

                if (!disconnected)
                {
                    foreach (var device in devices)
                    {
                        var deviceExists = _devices.Aggregate(false, (current, dev) => current | dev.DevicePath.Equals(device.DevicePath));

                        if (device == null || deviceExists) continue;

                        _devices.Add(device);
                        //device.OpenDevice();

                        //device.MonitorDeviceEvents = false;
                        //device.ReadReport(OnReport);
                        //device.CloseDevice();
                    }
                }
                _devices = devices;

                UpdateDeviceMenu(disconnected);

                if (_devices.Count == 0)
                {
                    notifyIconLayer.Icon = Resources.disconnected;
                    notifyIconLayer.Text = "No device detected";
                }
                else
                {
                    if (notifyIconLayer.Text == "No device detected" || notifyIconLayer.Text == "Monitoring Turned Off")
                    {
                        notifyIconLayer.Icon = Resources.baselayer;
                        notifyIconLayer.Text = "(Unknown)";
                    }
                }
            }
        }

        private void UpdateDeviceMenu(bool disconnected)
        {
            if (disconnected)
            {
                MenuItem dmi = null;
                foreach (MenuItem mi in miDevices.MenuItems)
                {
                    if (_devices.Find(x => x.DevicePath.Equals(mi.Name)) == null)
                    {
                        dmi = mi;
                    }
                }

                if (dmi != null)
                {
                    miDevices.MenuItems.Remove(dmi);
                }
            }
            else
            {
                foreach (HidDevice h in _devices)
                {
                    if (miDevices.MenuItems.Find(h.DevicePath,false).Length == 0)
                    {
                        MenuItem nmi = new MenuItem
                        {
                            Name = h.DevicePath,
                            Text = GetDeviceInfo(h)
                        };

                        nmi.Click += Device_Click;
                        miDevices.MenuItems.Add(nmi);
                    }
                }
            }

            if (_devices.Count == 0)
                return;

            bool _checked = false;
            {
                foreach (MenuItem mi in miDevices.MenuItems)
                {
                    if (mi.Checked)
                    {
                        _checked = true;
                    }
                }
            }

            if (!_checked)
            {
                Device_Click(miDevices.MenuItems[0], new EventArgs());
            }
        }

        private void Device_Click(object sender, EventArgs e)
        {
            foreach (MenuItem mi in miDevices.MenuItems)
            {
                if (sender == mi)
                {
                    if (!mi.Checked)
                    {
                        mi.Checked = true;
                        _ActiveDevice = _devices.Find(x => x.DevicePath.Equals(mi.Name));
                        _ActiveDevice.OpenDevice();

                        _ActiveDevice.MonitorDeviceEvents = false;
                        _ActiveDevice.ReadReport(OnReport);
                        _ActiveDevice.CloseDevice();
                    }
                }
            }
        }

        private void OnReport(HidReport report)
        {
            
            if (_monitorDevice)
            {
                var data = report.Data;

                var outputString = string.Empty;
                for (var i = 0; i < data.Length; i++)
                {
                    if ((char)data[i] == '\0')
                        break;
                    outputString += (char)data[i];
                }

                if(outputString.Length < 1)
                    return;

                string _layer = outputString.Trim();

                switch (_layer)
                {
                    case string s when s.StartsWith("Layer_MOUSE"):
                        notifyIconLayer.Icon = Resources.actionlayer;
                        notifyIconLayer.Text = "Mouse";
                        cm.RestoreCursorColor();
                        cm.ChangeCursorColor(Color.Crimson);
                        break;
                    case string s when s.StartsWith("Layer_POINTER"):
                        notifyIconLayer.Icon = Resources.numberlayer;
                        notifyIconLayer.Text = "Pointer";
                        cm.RestoreCursorColor();
                        cm.ChangeCursorColor(Color.Lime);
                        break;
                    case string s when s.StartsWith("Layer_"):
                        notifyIconLayer.Icon = Resources.baselayer;
                        notifyIconLayer.Text = _layer;
                        cm.RestoreCursorColor();
                        break;
                }

                outputString = string.Empty;

                _ActiveDevice.ReadReport(OnReport);
            }
        }

        private static IEnumerable<HidDevice> GetListableDevices() =>
            HidDevices.Enumerate()
                .Where(d => d.IsConnected)
                .Where(device => device.Capabilities.InputReportByteLength > 0)
                .Where(device => (ushort)device.Capabilities.UsagePage == 0xFF31)
                .Where(device => (ushort)device.Capabilities.Usage == 0x0074);


        public static string GetDeviceInfo(IHidDevice d)
        {
           return GetManufacturerString(d) + ":" + GetProductString(d) + ":" + Convert.ToString(d.Attributes.VendorId,16) + ":" + Convert.ToString(d.Attributes.ProductId,16) + ":" + Convert.ToString(d.Attributes.Version,16);
        }
        private static string GetProductString(IHidDevice d)
        {
            if (d == null) return "";
            d.ReadProduct(out var bs);
            return System.Text.Encoding.Default.GetString(bs.Where(b => b > 0).ToArray());
        }

        private static string GetManufacturerString(IHidDevice d)
        {
            if (d == null) return "";
            d.ReadManufacturer(out var bs);
            return System.Text.Encoding.Default.GetString(bs.Where(b => b > 0).ToArray());
        }
    }
}