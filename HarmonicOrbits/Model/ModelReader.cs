using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HarmonicOrbits
{
    /// <summary>Decodes a coefficient pack.</summary>
    public static class ModelReader
    {
        // "HORB"
        private const uint Magic = 0x42524F48;
        private const ushort SupportedVersion = 1;

        /// <summary>Reads all bodies from a pack stream.</summary>
        public static List<BodyModel> Read(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            // BinaryReader is little-endian, matching all KSP targets.
            using (var r = new BinaryReader(stream, new UTF8Encoding(false), true))
            {
                uint magic = r.ReadUInt32();
                if (magic != Magic)
                {
                    throw new InvalidDataException(string.Format(
                        "not a HarmonicOrbits pack: magic was 0x{0:X8}, expected 0x{1:X8}",
                        magic, Magic));
                }

                ushort version = r.ReadUInt16();
                if (version != SupportedVersion)
                {
                    throw new InvalidDataException(string.Format(
                        "pack format version {0}, this build reads {1}",
                        version, SupportedVersion));
                }

                int bodyCount = r.ReadUInt16();
                List<BodyModel> bodies = new List<BodyModel>(bodyCount);
                for (int i = 0; i < bodyCount; i++)
                {
                    bodies.Add(ReadBody(r));
                }
                return bodies;
            }
        }

        private static BodyModel ReadBody(BinaryReader r)
        {
            string name = r.ReadString();
            double epochJd = r.ReadDouble();
            double spanDays = r.ReadDouble();
            double gm = r.ReadDouble();

            int elementCount = r.ReadByte();
            if (elementCount != BodyModel.ElementCount)
            {
                throw new InvalidDataException(string.Format(
                    "{0}: pack holds {1} elements, this build expects {2}",
                    name, elementCount, BodyModel.ElementCount));
            }

            ElementSeries[] series = new ElementSeries[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                series[i] = ReadSeries(r);
            }
            return new BodyModel(name, epochJd, spanDays, gm, series);
        }

        private static ElementSeries ReadSeries(BinaryReader r)
        {
            byte flags = r.ReadByte();
            bool circulating = (flags & 0x01) != 0;

            int secularLength = r.ReadByte();
            double[] secular = ReadDoubles(r, secularLength);
            double constant = r.ReadDouble();

            int terms = r.ReadUInt16();
            double[] omegas = ReadDoubles(r, terms);
            double[] cos = ReadDoubles(r, terms);
            double[] sin = ReadDoubles(r, terms);

            return new ElementSeries(circulating, secular, constant, omegas, cos, sin);
        }

        private static double[] ReadDoubles(BinaryReader r, int count)
        {
            double[] values = new double[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = r.ReadDouble();
            }
            return values;
        }
    }
}
