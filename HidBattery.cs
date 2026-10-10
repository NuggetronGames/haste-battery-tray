using System.Text;
using HidSharp;

namespace HasteBattery;

public sealed record BatteryReading(int Percent, bool? Charging);

public static class HidBattery
{
    public static byte[] ParseHex(string hex)
    {
        var parts = hex.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
    }

    static IEnumerable<HidDevice> Candidates(AppSettings s)
    {
        foreach (var dev in DeviceList.Local.GetHidDevices(s.VendorId))
        {
            if (s.ProductIds.Count > 0 && !s.ProductIds.Contains(dev.ProductID)) continue;
            yield return dev;
        }
    }

    /// <summary>Send the probe to every matching HID interface and return the first valid battery reply.</summary>
    public static BatteryReading? TryRead(AppSettings s)
    {
        byte[] probe, header;
        try { probe = ParseHex(s.ProbeHex); header = ParseHex(s.HeaderHex); }
        catch { return null; }

        foreach (var dev in Candidates(s))
        {
            int outLen, inLen;
            try { outLen = dev.GetMaxOutputReportLength(); inLen = dev.GetMaxInputReportLength(); }
            catch { continue; }
            if (outLen <= 1 || inLen <= 1) continue;

            if (!dev.TryOpen(out HidStream stream)) continue;
            using (stream)
            {
                stream.ReadTimeout = 300;
                stream.WriteTimeout = 500;

                var req = new byte[outLen];                       // req[0] = report ID (0 = unnumbered)
                Array.Copy(probe, 0, req, 1, Math.Min(probe.Length, outLen - 1));
                try { stream.Write(req); }
                catch { continue; }

                var deadline = DateTime.UtcNow.AddMilliseconds(900);
                var buf = new byte[inLen];
                while (DateTime.UtcNow < deadline)
                {
                    int n;
                    try { n = stream.Read(buf); }
                    catch (TimeoutException) { continue; }
                    catch { break; }

                    var r = Parse(buf, n, header, s);
                    if (r != null) return r;
                }
            }
        }
        return null;
    }

    static BatteryReading? Parse(byte[] buf, int len, byte[] header, AppSettings s)
    {
        // HidSharp puts the report ID at index 0; some interfaces are unnumbered so try both alignments.
        for (int start = 0; start <= 1; start++)
        {
            if (start + header.Length > len) break;
            bool match = true;
            for (int i = 0; i < header.Length; i++)
                if (buf[start + i] != header[i]) { match = false; break; }
            if (!match) continue;

            int bi = start + s.BatteryOffset;
            if (bi >= len) continue;
            int pct = buf[bi];
            if (pct > 100) continue;

            bool? charging = null;
            if (s.ChargingOffset >= 0 && start + s.ChargingOffset < len)
                charging = buf[start + s.ChargingOffset] == 1;
            return new BatteryReading(pct, charging);
        }
        return null;
    }

    /// <summary>Dump every HyperX HID interface plus raw replies, so the right protocol can be identified.</summary>
    public static string Diagnostics(AppSettings s)
    {
        var log = new StringBuilder();
        log.AppendLine($"HasteBattery diagnostics {DateTime.Now:G}");
        log.AppendLine($"Vendor filter: 0x{s.VendorId:X4}  Probe: [{s.ProbeHex}]  Header: [{s.HeaderHex}]");
        log.AppendLine();

        var devices = DeviceList.Local.GetHidDevices(s.VendorId).ToList();
        if (devices.Count == 0) log.AppendLine("No HID devices found for this vendor ID.");

        byte[] probe;
        try { probe = ParseHex(s.ProbeHex); } catch { probe = Array.Empty<byte>(); }

        foreach (var dev in devices)
        {
            string name;
            try { name = dev.GetProductName(); } catch { name = "?"; }
            int inLen = 0, outLen = 0, featLen = 0;
            try { inLen = dev.GetMaxInputReportLength(); outLen = dev.GetMaxOutputReportLength(); featLen = dev.GetMaxFeatureReportLength(); } catch { }

            log.AppendLine($"== {name}  PID 0x{dev.ProductID:X4}");
            log.AppendLine($"   path: {dev.DevicePath}");
            log.AppendLine($"   max report lengths  in={inLen} out={outLen} feature={featLen}");

            if (!dev.TryOpen(out HidStream stream)) { log.AppendLine("   could not open (in use or no access)"); continue; }
            using (stream)
            {
                stream.ReadTimeout = 300;
                stream.WriteTimeout = 500;

                // Listen for ~12 s. On interfaces that accept output reports, re-send the probe every 2 s
                // (the mouse may be asleep at first). Move the mouse during this window.
                var req = new byte[Math.Max(outLen, 0)];
                bool canWrite = outLen > 1 && probe.Length > 0;
                if (canWrite) Array.Copy(probe, 0, req, 1, Math.Min(probe.Length, outLen - 1));

                var buf = new byte[Math.Max(inLen, 2)];
                var start = DateTime.UtcNow;
                var nextProbe = start;
                int packets = 0, repeats = 0;
                string? previous = null;
                while (inLen > 1 && (DateTime.UtcNow - start).TotalSeconds < 12 && packets < 60)
                {
                    if (canWrite && DateTime.UtcNow >= nextProbe)
                    {
                        try { stream.Write(req); log.AppendLine($"   [{(DateTime.UtcNow - start).TotalSeconds:0.0}s] sent probe"); }
                        catch (Exception ex) { log.AppendLine($"   write failed: {ex.Message}"); canWrite = false; }
                        nextProbe = DateTime.UtcNow.AddSeconds(2);
                    }

                    int n;
                    try { n = stream.Read(buf); }
                    catch (TimeoutException) { continue; }
                    catch (Exception ex) { log.AppendLine($"   read failed: {ex.Message}"); break; }

                    string line = BitConverter.ToString(buf, 0, Math.Min(n, 24)).Replace('-', ' ');
                    if (line == previous) { repeats++; continue; }   // collapse identical packets (mouse movement etc.)
                    previous = line;
                    packets++;
                    log.AppendLine($"   [{(DateTime.UtcNow - start).TotalSeconds:0.0}s] rx: {line}");
                }
                if (repeats > 0) log.AppendLine($"   ({repeats} identical repeated packets omitted)");
                if (packets == 0) log.AppendLine("   (no packets received)");
            }
            log.AppendLine();
        }

        var reading = TryRead(s);
        log.AppendLine(reading == null
            ? "Result: no battery reading parsed with current settings."
            : $"Result: {reading.Percent}%  charging={reading.Charging?.ToString() ?? "unknown"}");
        return log.ToString();
    }
}
