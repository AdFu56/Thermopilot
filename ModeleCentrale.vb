Imports System
Imports System.Collections.Generic
Imports System.Linq

' ═══════════════════════════════════════════════════════════════════════════════
'  TYPE DE CENTRALE
' ═══════════════════════════════════════════════════════════════════════════════

Public Enum TypeCentrale
    Keithley2701Ethernet   ' Keithley 2700/2701 — scan INIT:CONT ON + FETC?
    DAQ6510Ethernet        ' Keithley DAQ6510   — scan ROUTe:SCAN:CREate + INIT + FETC?
    Autre
End Enum

' ═══════════════════════════════════════════════════════════════════════════════
'  TYPE DE CARTE
' ═══════════════════════════════════════════════════════════════════════════════

Public Enum TypeCarte
    Module7706   ' Keithley 7706 — 20 voies + 2 sorties analogiques ±12V
    Module7700   ' Keithley 7700 — 20 voies, pas de sorties analogiques
    Autre
End Enum

' ═══════════════════════════════════════════════════════════════════════════════
'  PLAGE DE VOIES — ex : "101,103-106,108,110-118"
' ═══════════════════════════════════════════════════════════════════════════════

Public Class PlageVoies

    Public Property TexteOriginal As String = ""

    ''' <summary>Parse la chaîne et retourne la liste triée des numéros de voies.</summary>
    Public ReadOnly Property Numeros As List(Of Integer)
        Get
            Return Parser(TexteOriginal)
        End Get
    End Property

    Public ReadOnly Property EstValide As Boolean
        Get
            ' Chaîne vide = valide (signifie "aucune voie pour cette carte")
            If TexteOriginal.Trim() = "" Then Return True
            Try
                Dim nums = Parser(TexteOriginal)
                Return nums.Count > 0
            Catch
                Return False
            End Try
        End Get
    End Property

    Public ReadOnly Property EstVide As Boolean
        Get
            Return TexteOriginal.Trim() = ""
        End Get
    End Property

    Public ReadOnly Property MessageErreur As String
        Get
            If TexteOriginal.Trim() = "" Then Return ""   ' vide = pas d'erreur
            Try
                Parser(TexteOriginal)
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Get
    End Property

    Public Shared Function Parser(texte As String) As List(Of Integer)
        Dim result As New List(Of Integer)
        If texte Is Nothing OrElse texte.Trim() = "" Then Return result

        For Each segment In texte.Split(","c)
            Dim s = segment.Trim()
            If s = "" Then Continue For

            If s.Contains("-") Then
                Dim parties = s.Split("-"c)
                If parties.Length <> 2 Then
                    Throw New FormatException(String.Format("Segment invalide : '{0}'", s))
                End If
                Dim debut, fin As Integer
                If Not Integer.TryParse(parties(0).Trim(), debut) OrElse
                   Not Integer.TryParse(parties(1).Trim(), fin) Then
                    Throw New FormatException(String.Format("Valeurs non numériques dans : '{0}'", s))
                End If
                If debut > fin Then
                    Throw New FormatException(String.Format("Début > fin dans : '{0}'", s))
                End If
                For v = debut To fin
                    result.Add(v)
                Next
            Else
                Dim num As Integer
                If Not Integer.TryParse(s, num) Then
                    Throw New FormatException(String.Format("Valeur non numérique : '{0}'", s))
                End If
                result.Add(num)
            End If
        Next

        Return result.Distinct().OrderBy(Function(x) x).ToList()
    End Function

    ''' <summary>Convertit une liste de numéros en chaîne compacte (ex : 101-105,108,110-120).</summary>
    Public Shared Function VersTexte(numeros As IEnumerable(Of Integer)) As String
        Dim liste = numeros.OrderBy(Function(x) x).ToList()
        If liste.Count = 0 Then Return ""

        Dim segments As New List(Of String)
        Dim debut = liste(0)
        Dim prec  = liste(0)

        For i = 1 To liste.Count - 1
            If liste(i) = prec + 1 Then
                prec = liste(i)
            Else
                segments.Add(If(debut = prec, debut.ToString(), debut & "-" & prec))
                debut = liste(i) : prec = liste(i)
            End If
        Next
        segments.Add(If(debut = prec, debut.ToString(), debut & "-" & prec))
        Return String.Join(",", segments)
    End Function

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  CONFIGURATION D'UNE CARTE
' ═══════════════════════════════════════════════════════════════════════════════

Public Class ConfigCarte

    Public Property NumeroCarte   As Integer = 1
    Public Property Type          As TypeCarte = TypeCarte.Module7706
    Public Property PlageEntrees  As New PlageVoies()   ' voies mesure
    Public Property PlageSorties  As New PlageVoies()   ' sorties analogiques

    Public Sub New(numero As Integer)
        NumeroCarte = numero
        ' Valeurs par défaut selon numéro de carte
        Dim base = (numero - 1) * 100
        PlageEntrees.TexteOriginal = String.Format("{0}-{1}", base + 101, base + 120)
        PlageSorties.TexteOriginal = String.Format("{0}-{1}", base + 123, base + 124)
    End Sub

    Public ReadOnly Property LibelleType As String
        Get
            Select Case Type
                Case TypeCarte.Module7706 : Return "Module 7706 (sorties ±Us V)"
                Case TypeCarte.Module7700 : Return "Module 7700 (mesure seule)"
                Case Else                 : Return "Autre"
            End Select
        End Get
    End Property

    ''' <summary>True si cette carte dispose de sorties analogiques.</summary>
    Public ReadOnly Property ASorties As Boolean
        Get
            Return Type = TypeCarte.Module7706
        End Get
    End Property

    Public Sub SauverDansConfig(cfg As ConfigManager, sectionBase As String)
        Dim s = sectionBase & "_Carte" & NumeroCarte
        cfg.Set_(s, "Type",    CInt(Type))
        cfg.Set_(s, "Entrees", PlageEntrees.TexteOriginal)
        cfg.Set_(s, "Sorties", PlageSorties.TexteOriginal)
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager, sectionBase As String)
        Dim s = sectionBase & "_Carte" & NumeroCarte
        Type = CType(cfg.GetInt(s, "Type", 0), TypeCarte)
        Dim base = (NumeroCarte - 1) * 100
        PlageEntrees.TexteOriginal = cfg.Get_(s, "Entrees",
            String.Format("{0}-{1}", base + 101, base + 120))
        PlageSorties.TexteOriginal = cfg.Get_(s, "Sorties",
            String.Format("{0}-{1}", base + 123, base + 124))
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  COMMANDE SCPI — une ligne du tableau de détails
' ═══════════════════════════════════════════════════════════════════════════════

Public Class CommandeSCPI

    Public Enum CategorieCommande
        Initialisation
        ConfigTC
        ConfigTension
        ConfigSortie
        Scan
    End Enum

    Public Property Categorie    As CategorieCommande
    Public Property Commande     As String = ""
    Public Property Description  As String = ""
    Public Property EstModifiable As Boolean = True

    Private Shared Function Cmd(cat As CategorieCommande, commande As String,
                                 description As String,
                                 Optional modifiable As Boolean = True) As CommandeSCPI
        Dim c As New CommandeSCPI()
        c.Categorie     = cat
        c.Commande      = commande
        c.Description   = description
        c.EstModifiable = modifiable
        Return c
    End Function

    Public Shared Function ParDefaut() As List(Of CommandeSCPI)
        Dim ini = CategorieCommande.Initialisation
        Dim tc  = CategorieCommande.ConfigTC
        Dim ten = CategorieCommande.ConfigTension
        Dim sor = CategorieCommande.ConfigSortie

        Dim lst As New List(Of CommandeSCPI)

        ' ── Initialisation ──
        lst.Add(Cmd(ini, "*RST",             "Réinitialisation complète de l'instrument (remise à zéro de tous les paramètres)", False))
        lst.Add(Cmd(ini, "FORM:ELEM READ",   "Format de sortie : valeurs brutes uniquement (sans horodatage ni numéro de canal)", False))
        lst.Add(Cmd(ini, "TRAC:CLE",         "Vider le buffer de lecture interne avant un nouveau scan", False))
        lst.Add(Cmd(ini, "INIT:CONT OFF",    "Désactiver l'initiation continue — le scan démarre uniquement sur commande", False))
        lst.Add(Cmd(ini, "TRIG:COUN 1",      "Nombre de scans par déclenchement : 1 (un seul passage sur toutes les voies)", True))
        lst.Add(Cmd(ini, "ROUT:SCAN:TSO IMM","Démarrage immédiat du scan dès que la liste est définie", False))
        lst.Add(Cmd(ini, "ROUT:SCAN:LSEL INT","Activer le scanner interne — démarre les acquisitions cycliques (tac-tac)", False))
        lst.Add(Cmd(ini, "DELAI_LECTURE_MS=100",
                    "Délai (ms) entre l'envoi de Read? et la lecture de la réponse. " &
                    "Augmenter (ex: 200, 500) si des voies affichent ERR à partir d'un certain rang. " &
                    "Format : DELAI_LECTURE_MS=valeur", True))

        ' ── Configuration thermocouples ──
        lst.Add(Cmd(tc, "UNIT:TEMP C,(@{VOIES})",          "Unité de température : Celsius. {VOIES} = liste des voies TC", False))
        lst.Add(Cmd(tc, "FUNC 'TEMP',(@{VOIES})",          "Fonction de mesure : température. Appliqué sur toutes les voies TC", False))
        lst.Add(Cmd(tc, "TEMP:TRAN TC,(@{VOIES})",         "Type de transducteur : thermocouple (TC)", False))
        lst.Add(Cmd(tc, "TEMP:TC:TYPE {TC},(@{VOIES})",    "Type de thermocouple : {TC} = K, J, T, E, N, R, S ou B", False))
        lst.Add(Cmd(tc, "SENS:TEMP:APER 0.05,(@{VOIES})",  "Temps d'acquisition par voie TC : 0.05 s (compromis vitesse/précision)", True))

        ' ── Configuration tension DC (4-20mA via shunt) ──
        lst.Add(Cmd(ten, "FUNC 'VOLT:DC',(@{VOIES_V})",    "Fonction : tension continue. {VOIES_V} = voies à signal analogique", False))
        lst.Add(Cmd(ten, "VOLT:APER 0.05,(@{VOIES_V})",    "Temps d'acquisition par voie tension : 0.05 s", True))
        lst.Add(Cmd(ten, "VOLT:RANG:AUTO ON,(@{VOIES_V})", "Calibre automatique — adapte la plage de mesure à la tension présente", True))
        lst.Add(Cmd(ten, "VOLT:DIG 6,(@{VOIES_V})",        "Résolution : 6 chiffres significatifs", True))

        ' ── Configuration sorties analogiques ──
        lst.Add(Cmd(sor, "OUTP:VOLT {V}, (@{SORTIE})", "Appliquer une tension {V} en volts sur la sortie {SORTIE}. Plage : -12V à +12V (Module 7706)", False))
        lst.Add(Cmd(sor, "OUTP:VOLT 0.0, (@{SORTIE})", "Mettre une sortie à 0V (état OFF)", False))

        Return lst
    End Function
    Public Shared Function LibelleCategorie(cat As CategorieCommande) As String
        Select Case cat
            Case CategorieCommande.Initialisation : Return "Initialisation"
            Case CategorieCommande.ConfigTC       : Return "Configuration thermocouples"
            Case CategorieCommande.ConfigTension  : Return "Configuration tension DC (4-20 mA)"
            Case CategorieCommande.ConfigSortie   : Return "Configuration sorties analogiques"
            Case CategorieCommande.Scan           : Return "Scan"
            Case Else                              : Return "Autre"
        End Select
    End Function

    ''' <summary>
    ''' Commandes SCPI par défaut pour le Keithley DAQ6510 avec carte 7700.
    ''' Utilise ROUTe:SCAN:CREate + INIT au lieu de INIT:CONT ON.
    ''' </summary>
    Public Shared Function ParDefautDAQ6510() As List(Of CommandeSCPI)
        Dim ini = CategorieCommande.Initialisation
        Dim tc  = CategorieCommande.ConfigTC
        Dim ten = CategorieCommande.ConfigTension
        Dim scn = CategorieCommande.Scan

        Dim lst As New List(Of CommandeSCPI)

        ' ── Initialisation ──
        lst.Add(Cmd(ini, "*RST",
            "Réinitialisation complète du DAQ6510", False))
        lst.Add(Cmd(ini, "DELAI_LECTURE_MS=150",
            "Délai (ms) entre l'envoi de la commande et la lecture. " &
            "Augmenter (ex: 300) si des ERR apparaissent. " &
            "Format : DELAI_LECTURE_MS=valeur", True))

        ' ── Configuration thermocouples ──
        lst.Add(Cmd(tc, ":FUNCtion 'TEMPerature',(@{VOIES})",
            "Fonction de mesure : température sur les voies TC. {VOIES} = plage ex: 101:110", False))
        lst.Add(Cmd(tc, ":SENSe:TEMPerature:TRANsducer TCouple,(@{VOIES})",
            "Type de transducteur : thermocouple (TCouple)", False))
        lst.Add(Cmd(tc, ":SENSe:TEMPerature:TCouple:TYPE {TC},(@{VOIES})",
            "Type de TC : {TC} = K, J, T, E, N, R, S ou B", False))
        lst.Add(Cmd(tc, ":SENSe:TEMPerature:TCouple:RJUNction:RSELect INTernal,(@{VOIES})",
            "Jonction de référence interne (INTernal) — ou EXTernal si sonde externe", True))
        lst.Add(Cmd(tc, ":SENSe:TEMPerature:ODETector ON,(@{VOIES})",
            "Détection fil ouvert : ON — retourne 9.9E+37 si thermocouple débranché", True))

        ' ── Configuration tension DC (4-20mA via shunt) ──
        lst.Add(Cmd(ten, ":FUNCtion 'VOLTage:DC',(@{VOIES_V})",
            "Fonction tension continue sur les voies 4-20mA. {VOIES_V} = plage ex: 111:115", False))
        lst.Add(Cmd(ten, ":SENSe:VOLTage:DC:RANGe:AUTO ON,(@{VOIES_V})",
            "Calibre automatique tension DC", True))

        ' ── Configuration scan ROUTe ──
        lst.Add(Cmd(scn, ":ROUTe:SCAN:CREate (@{TOUTES_VOIES})",
            "Créer la liste de scan. {TOUTES_VOIES} = plage complète ex: 101:115. " &
            "Remplacé automatiquement par Thermopilot au démarrage.", False))
        lst.Add(Cmd(scn, ":ROUTe:SCAN:COUNt:SCAN INF",
            "Nombre de scans : infini (scan continu)", True))
        lst.Add(Cmd(scn, ":ROUTe:SCAN:INTerval 1.0",
            "Intervalle entre deux scans en secondes (adapté à l'intervalle Thermopilot)", True))
        lst.Add(Cmd(scn, ":ROUTe:SCAN:RESTart ON",
            "Redémarrer automatiquement le scan après chaque cycle", True))
        lst.Add(Cmd(scn, ":DISPlay:SCReen SWIPE_SCAN",
            "Afficher l'écran de suivi du scan sur le DAQ6510", True))

        Return lst
    End Function

End Class
