﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;

namespace isci.modulbasis
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
            
            while(true)
            {
                Zustand.Lesen();

                var erfüllteTransitionen = konfiguration.Ausführungstransitionen.Where(a => a.Eingangszustand == (System.Int32)Zustand.value);
                if (erfüllteTransitionen.Count<Ausführungstransition>() <= 0) continue;

                
                structure.Schreiben();

                Zustand.value = erfüllteTransitionen.First<Ausführungstransition>().Ausgangszustand;
                Zustand.Schreiben();
            }
        }
    }
}