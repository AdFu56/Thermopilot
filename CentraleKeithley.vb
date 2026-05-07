Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

' ═══════════════════════════════════════════════════════════════════════════════
'  CENTRALE KEITHLEY — encapsule une centrale et toutes ses voies/sorties
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Représente une centrale Keithley complète :
'''   - connexion TCP/IP propre
'''   - ses voies de mesure et sorties
'''   - son moteur d'acquisition individuel
''' Plusieurs instances peuvent tourner en parallèle.
''' </summary>
Public Class CentraleKeithley

    ' ─── Identification ───────────────────────────────────────────────────────

    ''' <summary>Numéro de la centrale (1, 2, 3…).</summary>
    Public Property Numero As Integer

    ''' <summary>Nom libre affiché dans les onglets et le CSV.</summary>
    Public Property Nom As String
        Get
            Return _nom
        End Get
        Set(value As String)
            _nom = If(value = "", "Centrale " & Numero.ToString(), value)
        End Set
    End Property
    Private _nom As String = ""

    Public ReadOnly Property NomAffiche As String
        Get
            Return If(_nom <> "", _nom, "Centrale " & Numero.ToString())
        End Get
    End Property

    ' ─── Connexion ────────────────────────────────────────────────────────────

    Public Property IPAddress   As String  = "192.168.0.3"
    Public Property Port        As Integer = 1394
    Public Property TimeoutMs   As Integer = 3000

    Public ReadOnly Property Keithley As KeithleyComm
        Get
            Return _keithley
        End Get
    End Property
    Private _keithley As New KeithleyComm()

    Public ReadOnly Property EstConnectee As Boolean
        Get
            Return _keithley.IsConnected
        End Get
    End Property

    ' ─── Voies et sorties ─────────────────────────────────────────────────────

    Public ReadOnly Property Voies As GestionVoies
        Get
            Return _voies
        End Get
    End Property
    Private _voies As New GestionVoies()

    ' ─── Acquisition ──────────────────────────────────────────────────────────

    Public Property IntervalleMsec As Integer = 5000

    ' Dernier horodatage d'acquisition
    Public Property DerniereScan As DateTime = DateTime.MinValue

    ' Événement déclenché après chaque scan réussi (thread non-UI)
    Public Event NouvellesMesures(centrale As CentraleKeithley, horodatage As DateTime)
    Public Event ErreurAcquisition(centrale As CentraleKeithley, message As String)

    Private _threadAcq  As Thread
    Private _annuler    As Boolean = False
    Public  Property EnAcquisition As Boolean = False
    ''' <summary>True si ConfigurerScan a déjà été appelé pour cette centrale.</summary>
    Private _scanConfigure As Boolean = False

    ' ─── Connexion / Déconnexion ──────────────────────────────────────────────

    Public Function Connecter() As Boolean
        _keithley.IPAddress   = IPAddress
        _keithley.Port        = Port
        _keithley.ReadTimeout = TimeoutMs
        If _keithley.Connect() Then
            _keithley.ConfigurerRelais(New List(Of Integer)())
            Return True
        End If
        Return False
    End Function

    ''' <summary>
    ''' Applique les paramètres personnalisés issus de FormDetailsCentrale.
    ''' En particulier le délai de lecture (DELAI_LECTURE_MS=xxx).
    ''' </summary>
    Public Sub AppliquerCommandesSCPI(commandes As List(Of CommandeSCPI))
        If commandes Is Nothing Then Return
        For Each cmd In commandes
            ' Traiter la ligne spéciale DELAI_LECTURE_MS=xxx
            If cmd.Commande.StartsWith("DELAI_LECTURE_MS=", StringComparison.OrdinalIgnoreCase) Then
                Dim parts = cmd.Commande.Split("="c)
                If parts.Length = 2 Then
                    Dim v As Integer
                    If Integer.TryParse(parts(1).Trim(), v) Then
                        _keithley.DelaiLectureMs = Math.Max(0, Math.Min(5000, v))
                    End If
                End If
            End If
        Next
    End Sub

    Public Sub Deconnecter()
        ArreterAcquisition()
        _keithley.Disconnect()
        _scanConfigure = False
    End Sub

    ' ─── Acquisition ──────────────────────────────────────────────────────────

    Public Sub DemarrerAcquisition()
        If EnAcquisition Then Return
        _annuler    = False
        EnAcquisition = True
        _threadAcq  = New Thread(AddressOf BoucleAcquisition) With {
            .IsBackground = True,
            .Name         = "Acq_C" & Numero.ToString()
        }
        _threadAcq.Start()
    End Sub

    Public Sub ArreterAcquisition()
        _annuler      = True
        EnAcquisition = False
    End Sub

    ''' <summary>
    ''' Déclenche une lecture unique de toutes les voies actives, en dehors du cycle d'acquisition.
    ''' Utilisé par FormValeursBrutes pour rafraîchir sans démarrer l'acquisition.
    ''' Bloquant — appeler depuis un thread de fond ou via Task.Run.
    ''' </summary>
    Public Function LireMesureInstantanee() As Boolean
        If Not EstConnectee Then Return False
        Try
            ' Si déjà en acquisition continue, FETC? suffit
            If EnAcquisition Then
                Dim rep = _keithley.LireScan()
                If Not String.IsNullOrWhiteSpace(rep) Then
                    _voies.ParseReponse(rep, DateTime.Now)
                    Return True
                End If
                Return False
            End If

            ' Pas en acquisition : reconfigurer le scan si pas encore fait,
            ' puis lire le buffer
            If Not _scanConfigure Then
                Dim voiesT As New List(Of String)
                Dim voiesD As New List(Of String)
                Dim typeTC = "K"
                For Each v In _voies.VoiesTemperature()
                    voiesT.Add(v.Numero.ToString())
                Next
                For Each v In _voies.VoiesDebit()
                    voiesD.Add(v.Numero.ToString())
                Next
                If voiesT.Count = 0 AndAlso voiesD.Count = 0 Then Return False
                AppliquerConfigScan(voiesT, voiesD, typeTC)
            End If

            ' 2. Lire le buffer maintenant rempli
            Dim reponse = _keithley.LireScan()
            If Not String.IsNullOrWhiteSpace(reponse) Then
                _voies.ParseReponse(reponse, DateTime.Now)
                DerniereScan = DateTime.Now
                Return True
            End If
        Catch
        End Try
        Return False
    End Function

    Private Sub BoucleAcquisition()
        Dim sw As New System.Diagnostics.Stopwatch()
        Do While Not _annuler
            sw.Restart()
            Try
                Dim t   = DateTime.Now
                Dim rep = _keithley.LireScan()
                If Not String.IsNullOrWhiteSpace(rep) Then
                    _voies.ParseReponse(rep, t)
                    DerniereScan = t
                    RaiseEvent NouvellesMesures(Me, t)
                End If
            Catch ex As Exception
                If Not _annuler Then
                    RaiseEvent ErreurAcquisition(Me, ex.Message)
                End If
            End Try
            Dim reste = CInt(IntervalleMsec - sw.ElapsedMilliseconds)
            If reste > 0 Then Thread.Sleep(reste)
        Loop
        EnAcquisition = False
    End Sub

    ' ─── Config Keithley (scan) ───────────────────────────────────────────────

    Public Sub AppliquerConfigScan(voiesTemp As List(Of String), voiesDebit As List(Of String), typeTC As String)
        If Not EstConnectee Then Return
        _keithley.ConfigurerScan(
            String.Join(",", voiesTemp),
            String.Join(",", voiesDebit),
            typeTC)
        _scanConfigure = True
    End Sub

    ' Surcharge legacy (sans listes)
    Public Sub AppliquerConfigScan(typeTC As String)
        If Not EstConnectee Then Return
        Dim voiesT As New List(Of String)
        Dim voiesD As New List(Of String)
        For Each v In _voies.VoiesTemperature()
            voiesT.Add(v.Numero.ToString())
        Next
        For Each v In _voies.VoiesDebit()
            voiesD.Add(v.Numero.ToString())
        Next
        _keithley.ConfigurerScan(String.Join(",", voiesT), String.Join(",", voiesD), typeTC)
    End Sub

    ' ─── Persistance config ───────────────────────────────────────────────────

    Public ReadOnly Property SectionIni As String
        Get
            Return "Centrale" & Numero.ToString()
        End Get
    End Property

    Public Sub SauverConnexionDansConfig(cfg As ConfigManager)
        cfg.Set_(SectionIni, "Nom",       NomAffiche)
        cfg.Set_(SectionIni, "IPAddress", IPAddress)
        cfg.Set_(SectionIni, "Port",      Port)
        cfg.Set_(SectionIni, "Timeout",   TimeoutMs)
    End Sub

    Public Sub ChargerConnexionDepuisConfig(cfg As ConfigManager)
        Nom       = cfg.Get_(SectionIni, "Nom",       "Centrale " & Numero.ToString())
        IPAddress = cfg.Get_(SectionIni, "IPAddress", "192.168.0." & (Numero + 2).ToString())
        Port      = cfg.GetInt(SectionIni, "Port",    1394)
        TimeoutMs = cfg.GetInt(SectionIni, "Timeout", 3000)
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  GESTIONNAIRE MULTI-CENTRALE
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Coordonne N centrales Keithley :
'''   - création/suppression dynamique
'''   - lancement parallèle des acquisitions
'''   - fusion des données pour le CSV et le graphique
''' </summary>
Public Class GestionnaireMultiCentrale

    Private _centrales As New List(Of CentraleKeithley)
    Private _lock      As New Object()

    ''' <summary>Référence au gestionnaire de voies calculées (pour l'export CSV).</summary>
    Public Property GestCalculs As GestionnaireCalculs = Nothing
    ''' <summary>
    ''' Format .NET utilisé pour les valeurs numériques dans le CSV.
    ''' Alimenté par OngletCSV.FormatValeur. Défaut : "F3".
    ''' </summary>
    Public Property FormatCSV As String = "F3"

    ' ─── Événements agrégés ───────────────────────────────────────────────────

    ''' <summary>Déclenché quand une centrale a de nouvelles mesures.</summary>
    Public Event NouvellesMesures(centrale As CentraleKeithley, horodatage As DateTime)
    Public Event ErreurAcquisition(centrale As CentraleKeithley, message As String)

    ' ─── Accès aux centrales ──────────────────────────────────────────────────

    Public ReadOnly Property Centrales As IReadOnlyList(Of CentraleKeithley)
        Get
            Return _centrales.AsReadOnly()
        End Get
    End Property

    Public ReadOnly Property NbCentrales As Integer
        Get
            Return _centrales.Count
        End Get
    End Property

    Public Function ObtenirCentrale(numero As Integer) As CentraleKeithley
        Return _centrales.FirstOrDefault(Function(c) c.Numero = numero)
    End Function

    ' ─── Gestion du nombre de centrales ──────────────────────────────────────

    ''' <summary>
    ''' Ajuste le nombre de centrales.
    ''' Ajoute les nouvelles, supprime les excédentaires (après déconnexion).
    ''' </summary>
    Public Sub DefinirNombreCentrales(nb As Integer, cfg As ConfigManager)
        SyncLock _lock
            ' Supprimer les centrales en trop
            Do While _centrales.Count > nb
                Dim c = _centrales(_centrales.Count - 1)
                c.Deconnecter()
                RemoveHandler c.NouvellesMesures, AddressOf OnNouvellesMesures
                RemoveHandler c.ErreurAcquisition, AddressOf OnErreurAcquisition
                _centrales.RemoveAt(_centrales.Count - 1)
            Loop

            ' Ajouter les nouvelles
            Do While _centrales.Count < nb
                Dim numero = _centrales.Count + 1
                Dim c As New CentraleKeithley() With {.Numero = numero}
                c.ChargerConnexionDepuisConfig(cfg)
                AddHandler c.NouvellesMesures, AddressOf OnNouvellesMesures
                AddHandler c.ErreurAcquisition, AddressOf OnErreurAcquisition
                _centrales.Add(c)
            Loop
        End SyncLock
    End Sub

    ' ─── Connexions ───────────────────────────────────────────────────────────

    Public Function ConnecterToutes() As Dictionary(Of Integer, Boolean)
        Dim resultats As New Dictionary(Of Integer, Boolean)
        For Each c In _centrales
            resultats(c.Numero) = c.Connecter()
        Next
        Return resultats
    End Function

    Public Sub DeconnecterToutes()
        For Each c In _centrales
            c.Deconnecter()
        Next
    End Sub

    Public ReadOnly Property NbConnectees As Integer
        Get
            Return _centrales.Where(Function(c) c.EstConnectee).Count()
        End Get
    End Property

    ' ─── Acquisition ──────────────────────────────────────────────────────────

    Public Sub DemarrerAcquisitionToutes()
        For Each c In _centrales.Where(Function(x) x.EstConnectee)
            c.DemarrerAcquisition()
        Next
    End Sub

    Public Sub ArreterAcquisitionToutes()
        For Each c In _centrales
            c.ArreterAcquisition()
        Next
    End Sub

    Public ReadOnly Property NbEnAcquisition As Integer
        Get
            Return _centrales.Where(Function(c) c.EnAcquisition).Count()
        End Get
    End Property

    ' ─── Relais (toutes centrales) ────────────────────────────────────────────

    ''' <summary>
    ''' Liste consolidée de toutes les sorties actives de toutes les centrales.
    ''' Chaque sortie porte la référence à sa centrale.
    ''' </summary>
    Public Function ToutesSortiesActives() As List(Of (Centrale As CentraleKeithley, Sortie As SortieAnalogique))
        Dim liste As New List(Of (CentraleKeithley, SortieAnalogique))
        For Each c In _centrales
            For Each s In c.Voies.SortiesActives()
                liste.Add((c, s))
            Next
        Next
        Return liste
    End Function

    ''' <summary>Toutes les voies actives de toutes les centrales.</summary>
    Public Function ToutesVoiesActives() As List(Of (Centrale As CentraleKeithley, Voie As VoieMesure))
        Dim liste As New List(Of (CentraleKeithley, VoieMesure))()
        For Each c In _centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                liste.Add((c, v))
            Next
        Next
        Return liste
    End Function

    ' ─── CSV multi-centrale ───────────────────────────────────────────────────

    ''' <summary>
    ''' Libellé de la colonne durée (ex: "Durée (s)"). Alimenté par OngletCSV.
    ''' </summary>
    Public Property LibelleUniteDuree As String = "Durée (s)"

    ''' <summary>
    ''' Diviseur pour convertir les secondes dans l'unité choisie (1=s, 60=min, 3600=h).
    ''' Alimenté par OngletCSV.
    ''' </summary>
    Public Property DiviseurDuree As Double = 1.0

    ''' <summary>Horodatage du démarrage de l'acquisition, pour le calcul de la durée.</summary>
    Public Property HeureDepart As DateTime = DateTime.MinValue

    Public Function EnteteCSV() As String
        Dim cols As New List(Of String) From {"Horodatage", LibelleUniteDuree}
        For Each c In _centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                cols.Add(String.Format("{0}_{1} ({2})", c.NomAffiche, v.Nom, v.Unite))
            Next
            For Each s In c.Voies.SortiesActives()
                Dim unite = If(s.Mode = SortieAnalogique.ModePilotage.Analogique OrElse
                               s.Mode = SortieAnalogique.ModePilotage.AnalogiqueFull, "V", "ON/OFF")
                cols.Add(String.Format("{0}_{1} ({2})", c.NomAffiche, s.Nom, unite))
            Next
        Next
        ' Voies calculées
        If GestCalculs IsNot Nothing Then
            For Each vc In GestCalculs.Voies.Where(Function(v) v.Active)
                cols.Add(String.Format("[Calcul] {0} ({1})", vc.Nom, vc.Unite))
            Next
        End If
        ' Colonne Notification — toujours en dernière position
        cols.Add("Notification")
        Return String.Join(OngletCSV.SEPARATEUR, cols)
    End Function

    ''' <summary>
    ''' Ligne CSV à l'horodatage courant.
    ''' Les voies non encore mesurées reçoivent "---".
    ''' </summary>
    Public Function LigneCSV(horodatage As DateTime,
                              Optional notification As String = "") As String
        Dim cols As New List(Of String)
        cols.Add(horodatage.ToString("yyyy-MM-dd HH:mm:ss"))
        ' Durée depuis le démarrage de l'acquisition
        Dim duree As Double = 0.0
        If HeureDepart <> DateTime.MinValue Then
            duree = (horodatage - HeureDepart).TotalSeconds / DiviseurDuree
        End If
        cols.Add(duree.ToString("G", System.Globalization.CultureInfo.InvariantCulture))
        Dim fmt = FormatCSV
        For Each c In _centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                If Double.IsNaN(v.Valeur) OrElse v.EnErreur Then
                    cols.Add("ERREUR")
                Else
                    cols.Add(v.Valeur.ToString(fmt,
                        System.Globalization.CultureInfo.InvariantCulture))
                End If
            Next
            For Each s In c.Voies.SortiesActives()
                If s.Mode = SortieAnalogique.ModePilotage.Analogique OrElse
                   s.Mode = SortieAnalogique.ModePilotage.AnalogiqueFull Then
                    cols.Add(s.TensionV.ToString(fmt,
                        System.Globalization.CultureInfo.InvariantCulture))
                Else
                    cols.Add(If(s.EstOn, "1", "0"))
                End If
            Next
        Next
        ' Voies calculées
        If GestCalculs IsNot Nothing Then
            For Each vc In GestCalculs.Voies.Where(Function(v) v.Active)
                If vc.EnErreur OrElse Double.IsNaN(vc.Valeur) Then
                    cols.Add("ERR")
                Else
                    cols.Add(vc.Valeur.ToString(fmt,
                        System.Globalization.CultureInfo.InvariantCulture))
                End If
            Next
        End If
        ' Colonne Notification (vide en acquisition normale)
        If notification <> "" Then
            cols.Add("""" & notification.Replace("""", """""") & """")
        Else
            cols.Add("")
        End If
        Return String.Join(OngletCSV.SEPARATEUR, cols)
    End Function

    ' ─── Persistance ──────────────────────────────────────────────────────────

    Public Sub SauverDansConfig(cfg As ConfigManager)
        cfg.Set_(ConfigManager.SEC_CONNEXION, "NbCentrales", _centrales.Count)
        For Each c In _centrales
            c.SauverConnexionDansConfig(cfg)
        Next
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        Dim nb = cfg.GetInt(ConfigManager.SEC_CONNEXION, "NbCentrales", 1)
        DefinirNombreCentrales(nb, cfg)
    End Sub

    ' ─── Événements internes ──────────────────────────────────────────────────

    Private Sub OnNouvellesMesures(centrale As CentraleKeithley, horodatage As DateTime)
        RaiseEvent NouvellesMesures(centrale, horodatage)
    End Sub

    Private Sub OnErreurAcquisition(centrale As CentraleKeithley, message As String)
        RaiseEvent ErreurAcquisition(centrale, message)
    End Sub

End Class
