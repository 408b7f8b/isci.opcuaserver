﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;

namespace isci.opcuaserver
{
    public class Konfiguration : Parameter
    {
        public Konfiguration(string datei) : base(datei) {

        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var konfiguration = new Konfiguration("konfiguration.json");
            
            var structure = new Datenstruktur(konfiguration.OrdnerDatenstruktur);

            var beschreibung = new Modul(konfiguration.Identifikation, "isci.opcauserver", new ListeDateneintraege(){});
            beschreibung.Name = "OPC-UA-Server Ressource " + konfiguration.Identifikation;
            beschreibung.Beschreibung = "OPC-UA-Server";
            beschreibung.Speichern(konfiguration.OrdnerBeschreibungen + "/" + konfiguration.Identifikation + ".json");

            structure.DatenmodelleEinhängenAusOrdner(konfiguration.OrdnerDatenmodelle);
            structure.Start();

            var Zustand = new dtZustand(konfiguration.OrdnerDatenstruktur);
            Zustand.Start();

            var server = new OpcUaServer();
            server.StrukturEintragen(structure);
            server.Start();
            server.DatenstrukturAufbauen();

            if (konfiguration.Ausführungstransitionen != null)
            {
                while(true)
                {
                    System.Threading.Thread.Sleep(1000);

                    Zustand.Lesen();

                    var erfüllteTransitionen = konfiguration.Ausführungstransitionen.Where(a => a.Eingangszustand == (System.Int32)Zustand.value);
                    if (erfüllteTransitionen.Count<Ausführungstransition>() <= 0) continue;

                    if (erfüllteTransitionen.ElementAt(0) == konfiguration.Ausführungstransitionen[0])
                    {
                        while (server.puffer_mutex)
                        {

                        }

                        server.puffer_mutex = true;

                        foreach (var neuer_wert in server.puffer)
                        {
                            structure.dateneinträge[neuer_wert.Key].value = neuer_wert.Value;
                        }

                        server.puffer.Clear();

                        server.puffer_mutex = false;

                        structure.Schreiben();
                    } else if (erfüllteTransitionen.ElementAt(0) == konfiguration.Ausführungstransitionen[1])
                    {
                        var aenderungen = structure.Lesen();                    

                        foreach (var geandert in aenderungen)
                        {
                            server.DatenwertVerbreiten(geandert);
                        }

                        structure.AenderungenZuruecksetzen(aenderungen);
                    }

                    Zustand.value = erfüllteTransitionen.First<Ausführungstransition>().Ausgangszustand;
                    Zustand.Schreiben();
                }
            } else {
                while(true)
                {
                    System.Threading.Thread.Sleep(50);

                    while (server.puffer_mutex)
                    {

                    }

                    server.puffer_mutex = true;

                    foreach (var neuer_wert in server.puffer)
                    {
                        structure.dateneinträge[neuer_wert.Key].value = neuer_wert.Value;
                    }

                    server.puffer.Clear();

                    server.puffer_mutex = false;

                    structure.Schreiben();

                    System.Threading.Thread.Sleep(50);

                    var aenderungen = structure.Lesen();                    

                    foreach (var geandert in aenderungen)
                    {
                        server.DatenwertVerbreiten(geandert);
                    }

                    structure.AenderungenZuruecksetzen(aenderungen);
                }
            }
        }
    }
}