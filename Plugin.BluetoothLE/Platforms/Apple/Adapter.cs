using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using CoreBluetooth;


namespace Plugin.BluetoothLE
{
    public partial class Adapter : AbstractAdapter
    {
        readonly AdapterContext context;


        public override string DeviceName => "Default Bluetooth Device";
        public override bool IsScanning => this.context.Manager.IsScanning;


        public override AdapterStatus Status
        {
            get
            {
                switch (this.context.Manager.State)
                {
                    //case CBCentralManagerState.PoweredOff:
                    case CBManagerState.PoweredOff:
                        return AdapterStatus.PoweredOff;

                    //case CBCentralManagerState.PoweredOn:
                    case CBManagerState.PoweredOn:
                        return AdapterStatus.PoweredOn;

                    //case CBCentralManagerState.Resetting:
                    case CBManagerState.Resetting:
                        return AdapterStatus.Resetting;

                    //case CBCentralManagerState.Unauthorized:
                    case CBManagerState.Unauthorized:
                        return AdapterStatus.Unauthorized;

                    //case CBCentralManagerState.Unsupported:
                    case CBManagerState.Unsupported:
                        return AdapterStatus.Unsupported;

                    //case CBCentralManagerState.Unknown:
                    case CBManagerState.Unknown:
                    default:
                        return AdapterStatus.Unknown;
                }
            }
        }


        public override IObservable<IDevice> GetKnownDevice(Guid deviceId)
        {
            var peripheral = this.context.Manager.RetrievePeripheralsWithIdentifiers(deviceId.ToNSUuid()).FirstOrDefault();
            if (peripheral == null)
                return Observable.Return<IDevice>(null);

            var device = this.context.GetDevice(peripheral);
            return Observable.Return(device);
        }


        public override IObservable<IEnumerable<IDevice>> GetPairedDevices() => Observable.Return(new IDevice[0]);

        public override IObservable<IEnumerable<IDevice>> GetConnectedDevices(Guid? serviceUuid = null)
        {
            if (serviceUuid == null)
                return Observable.Return(this.context.GetConnectedDevices().ToList());

            var list = new List<IDevice>();
            var peripherals = this.context.Manager.RetrieveConnectedPeripherals(serviceUuid.Value.ToCBUuid());
            foreach (var peripheral in peripherals)
            {
                var dev = this.context.GetDevice(peripheral);
                list.Add(dev);
            }
            return Observable.Return(list);
        }


        public override IObservable<AdapterStatus> WhenStatusChanged() => this.context
            .StateUpdated
            .Select(_ => this.Status)
            .StartWith(this.Status);


        public override IObservable<IScanResult> Scan(ScanConfig config)
        {
            config = config ?? new ScanConfig();

            if (this.Status != AdapterStatus.PoweredOn)
                throw new ArgumentException("Your adapter status is " + this.Status);

            if (this.IsScanning)
                throw new ArgumentException("There is already an existing scan");

            if (config.ScanType == BleScanType.Background && (config.ServiceUuids == null || config.ServiceUuids.Count == 0))
                throw new ArgumentException("Background scan type set but not ServiceUUID");

            return Observable.Create<IScanResult>(ob =>
            {
                this.context.Clear();
                var scan = this.context
                    .ScanResultReceived
                    .AsObservable()
                    .Subscribe(ob.OnNext);

                if (config.ServiceUuids == null || config.ServiceUuids.Count == 0)
                {
                    this.context.Manager.ScanForPeripherals(null, new PeripheralScanningOptions { AllowDuplicatesKey = true });
                }
                else
                {
                    var uuids = config.ServiceUuids.Select(o => o.ToCBUuid()).ToArray();
                    if (config.ScanType == BleScanType.Background)
                    {
                        this.context.Manager.ScanForPeripherals(uuids);
                    }
                    else
                    {
                        this.context.Manager.ScanForPeripherals(uuids, new PeripheralScanningOptions { AllowDuplicatesKey = true });
                    }
                }

                return () =>
                {
                    this.context.Manager.StopScan();
                    scan.Dispose();
                };
            });
        }


        public override void StopScan() => this.context.Manager.StopScan();


        //IObservable<IDevice> deviceStatusOb;
        //public override IObservable<IDevice> WhenDeviceStatusChanged()
        //{
        //    this.deviceStatusOb = this.deviceStatusOb ??
        //        this.context
        //            .PeripheralConnected
        //            .Select(x => this.context.GetDevice(x))
        //            .Merge(this.context
        //                .PeripheralDisconnected
        //                .Select(x => this.context.GetDevice(x))
        //            )
        //            .Publish()
        //            .RefCount();

        //    return this.deviceStatusOb;
        //}


        public override IObservable<IDevice> WhenDeviceStateRestored() =>
            this.context
                .WhenWillRestoreState
                .AsObservable();
	}
}