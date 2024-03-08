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
        public Konfiguration(string[] args) : base(args) {

        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var konfiguration = new Konfiguration(args);

            var structure = new Datenstruktur(konfiguration);
            var ausfuehrungsmodell = new Ausführungsmodell(konfiguration, structure.Zustand);

            var beschreibung = new Modul(konfiguration.Identifikation, "isci.opcauserver")
            {
                Name = "OPC-UA-Server Ressource " + konfiguration.Identifikation,
                Beschreibung = "OPC-UA-Server"
            };
            beschreibung.Speichern(konfiguration);

            structure.DatenmodelleEinhängenAusOrdner(konfiguration.OrdnerDatenmodelle);
            structure.Start();

            var server = new OpcUaServer()
            {
                name = konfiguration.Anwendung
            };
            server.StrukturEintragen(structure);
            server.Start();
            server.DatenstrukturAufbauen();

            while (true)
            {
                structure.Zustand.WertAusSpeicherLesen();

                if (ausfuehrungsmodell.AktuellerZustandModulAktivieren())
                {
                    var schritt_param = ausfuehrungsmodell.ParameterAktuellerZustand();

                    switch (schritt_param)
                    {
                        case "E":
                        {
                            while (server.puffer_mutex)
                            {

                            }

                            server.puffer_mutex = true;

                            foreach (var neuer_wert in server.puffer)
                            {
                                structure.dateneinträge[neuer_wert.Key].Wert = neuer_wert.Value;
                            }

                            server.puffer.Clear();

                            server.puffer_mutex = false;

                            structure.Schreiben();
                            break;
                            }
                        case "A":
                        {
                            var aenderungen = structure.Lesen();

                            foreach (var geandert in aenderungen)
                            {
                                server.DatenwertVerbreiten(geandert);
                            }

                            structure.AenderungenZuruecksetzen(aenderungen);
                            break;
                        }
                    }

                    structure.Zustand++;
                    structure.Zustand.WertInSpeicherSchreiben();
                }

                Helfer.SleepForMicroseconds(konfiguration.PauseArbeitsschleifeUs);
            }
        }
    }
}