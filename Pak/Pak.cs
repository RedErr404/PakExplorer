﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace PakExplorer.Pak;

public class Pak {
    public string FileName;
    public List<PakEntry> PakEntries = new();
    private BinaryReader? _reader;
    
    private int _formSize;
    private int _dataSize;
    private int _entriesSize;
    
    public Pak(string src) {
        FileName = src;
        using (_reader = new BinaryReader(File.OpenRead(src))) {
            ReadForm();
            ReadHead();
            ReadData();
            ReadEntries();
            ReadEntryData();
            _reader = null;
        }
    }

    private void ReadForm() {
        _reader.SkipPakSignature(); // "FORM"
        _formSize = _reader.ReadInt32BE(); // form Size
        _reader.SkipPakSignature(); // "PAC1"
    }

    private void ReadHead() {
        _reader.SkipPakSignature(); // "HEAD"
        _reader.Skip(32); // Constant Unknown
    }

    private void ReadData() {
        _reader.SkipPakSignature(); // "DATA"
        _dataSize = _reader.ReadInt32BE(); // data Size
        _reader.Skip(_dataSize); // skip data for now
    }

    private void ReadEntries() {
        _reader.SkipPakSignature(); // "FILE"
        _entriesSize = _reader.ReadInt32BE(); // PakEntries Size ( not count )

        try {
            _reader.Skip(2); // Null
            _reader.Skip(4); // Constant Unknown

            for ( var posEntries = _reader.BaseStream.Position; _reader.BaseStream.Position - posEntries < _entriesSize; ) {
                var entryType = (PakEntryType)_reader.ReadByte();
                int entryNameLength = _reader.ReadByte();
                var entryNameBytes = new byte[entryNameLength];
                _reader.Read(entryNameBytes, 0, entryNameLength);
                var entryName = Encoding.UTF8.GetString(entryNameBytes);

                if ( entryType == PakEntryType.Directory ) ReadEntriesFromDirectory(entryName);
                else PakEntries.Add(new PakEntry(entryName, _reader));
            }
        }
        catch {}
    }

    private void ReadEntriesFromDirectory(string dirName) {
        var childCount = _reader.ReadInt32();

        for ( var i = 0; i < childCount; i++ ) {
	        var entryType = (PakEntryType)_reader.ReadByte();
            int entryNameLength = _reader.ReadByte();
            var entryNameBytes = new byte[entryNameLength];
            _reader.Read(entryNameBytes, 0, entryNameLength);
            var entryName = dirName + "\\" + Encoding.UTF8.GetString(entryNameBytes); ;

            switch (entryType) {
                case PakEntryType.File:
                    PakEntries.Add(new PakEntry(entryName, _reader));
                    break;
                case PakEntryType.Directory:
                    ReadEntriesFromDirectory(entryName);
                    break;
                default:
                    throw new Exception("Unknown Entry Type");
            }
        }
    }

    private void ReadEntryData() {
        foreach (var entry in PakEntries) {
            if (entry.CompressionType == PakCompressionType.Zlib) {
                try {
                    _reader.BaseStream.Position = entry.BinaryOffset;
                    entry.EntryData = _reader.ReadCompressedData(entry.OriginalSize);
                    continue;
                } catch {
                    _reader.BaseStream.Position = entry.BinaryOffset;
                    entry.EntryData = _reader.ReadBytes(entry.Size);
                    continue;
                }
            } else {
                _reader.BaseStream.Position = entry.BinaryOffset;
                entry.EntryData = _reader.ReadBytes(entry.Size);
                continue;
            }
        }
    }
}