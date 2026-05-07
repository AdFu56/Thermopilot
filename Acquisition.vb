Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

' ═══════════════════════════════════════════════════════════════════════════════
'  MOTEUR D'ACQUISITION MULTI-CENTRALE
' ═══════════════════════════════════════════════════════════════════════════════

Public Class MoteurAcquisition

    Public Event NouvellesMesures(sender As Object, centrale As CentraleKeithley, horodatage As DateTime)
    Public Event ErreurAcquisition(sender As Object, message As String)
    Public Event LigneCSVEcrite(sender As Object, centrale As CentraleKeithley)

    Public Property Gestionnaire        As GestionnaireMultiCentrale
    Public Property IntervalleMsec      As Integer = 5000
    Public Property GestCalculs         As GestionnaireCalculs = Nothing
    Public Property Historique          As HistoriqueMultiCentrale = Nothing
    Public Property StockerCSV          As Boolean = True
    Public Property CheminCSV           As String  = ""
    Public Property FormulairePrincipal As Form

    ' Mode simulation
    Public Enum ModeSimulation
        Desactive
        MonteeEnTemperature
    End Enum
    Public Property ModeSim As ModeSimulation = ModeSimulation.Desactive

    Public ReadOnly Property EstEnSimulation As Boolean
        Get
            Return ModeSim <> ModeSimulation.Desactive
        End Get
    End Property

    Public Property EnCours       As Boolean = False
    Public Property NombreMesures As Long    = 0

    ' ─── File d'attente notification ─────────────────────────────────────────
    ' La notification est stockée ici après validation par l'utilisateur,
    ' puis consommée (vidée) lors de la prochaine écriture CSV.
    Private _notificationEnAttente As String = ""
    Private _lockNotif             As New Object()

    ''' <summary>
    ''' Met une notification en attente d'écriture CSV.
    ''' Elle sera incluse dans la prochaine ligne de mesure.
    ''' </summary>
    Public Sub MettreNotificationEnAttente(texte As String)
        SyncLock _lockNotif
            _notificationEnAttente = texte
        End SyncLock
    End Sub

    Private _annuler   As Boolean = False
    Private _writerCSV As StreamWriter
    Private _csvInit   As Boolean = False
    Private _lockCSV   As New Object()
    Private _rng       As New Random()
    Private _simDepart As DateTime

    ' ─── Démarrage / Arrêt ──────────────────────────────────────────────────

    Public Function Demarrer() As Boolean
        If EnCours Then Return True

        If ModeSim = ModeSimulation.Desactive Then
            If Gestionnaire Is Nothing OrElse Gestionnaire.NbConnectees = 0 Then
                RaiseEvent ErreurAcquisition(Me, "Aucune centrale connectée")
                Return False
            End If
        End If

        If StockerCSV Then
            If Not InitCSV() Then Return False
        End If

        _annuler   = False
        _simDepart = DateTime.Now
        NombreMesures = 0

        ' Configurer l'intervalle sur chaque centrale et démarrer
        If ModeSim = ModeSimulation.Desactive Then
            For Each c In Gestionnaire.Centrales
                If c.EstConnectee Then
                    c.IntervalleMsec = IntervalleMsec
                End If
            Next
            ' S'abonner aux événements de chaque centrale
            For Each c In Gestionnaire.Centrales
                AddHandler c.NouvellesMesures, AddressOf Centrale_NouvellesMesures
                AddHandler c.ErreurAcquisition, AddressOf Centrale_Erreur
            Next
            Gestionnaire.DemarrerAcquisitionToutes()
        Else
            ' Mode simulation : un seul thread pour toutes les centrales simulées
            Dim t As New Thread(AddressOf BoucleSimulation) With {
                .IsBackground = True, .Name = "SimuAcq"
            }
            t.Start()
        End If

        EnCours = True
        Return True
    End Function

    Public Sub Arreter()
        _annuler = True
        EnCours  = False

        If Gestionnaire IsNot Nothing Then
            For Each c In Gestionnaire.Centrales
                RemoveHandler c.NouvellesMesures, AddressOf Centrale_NouvellesMesures
                RemoveHandler c.ErreurAcquisition, AddressOf Centrale_Erreur
            Next
            Gestionnaire.ArreterAcquisitionToutes()
        End If
        FermerCSV()
    End Sub

    ' ─── Réception depuis les centrales réelles ───────────────────────────────

    Private Sub Centrale_NouvellesMesures(centrale As CentraleKeithley, horodatage As DateTime)
        NombreMesures += 1
        ' Calculer les voies calculées AVANT d'écrire le CSV
        If GestCalculs IsNot Nothing AndAlso Historique IsNot Nothing Then
            Dim dtSec = IntervalleMsec / 1000.0
            Historique.AjouterMesuresCentrale(centrale, horodatage)
            GestCalculs.CalculerEtInjecter(Historique, horodatage, dtSec)
        End If
        If StockerCSV AndAlso _csvInit Then
            EcrireCSV(centrale, horodatage)
        End If
        If FormulairePrincipal IsNot Nothing AndAlso Not FormulairePrincipal.IsDisposed Then
            FormulairePrincipal.BeginInvoke(Sub() RaiseEvent NouvellesMesures(Me, centrale, horodatage))
        End If
    End Sub

    Private Sub Centrale_Erreur(centrale As CentraleKeithley, message As String)
        If Not _annuler Then
            RaiseEvent ErreurAcquisition(Me, String.Format("[C{0}] {1}", centrale.Numero, message))
        End If
    End Sub

    ' ─── Simulation ───────────────────────────────────────────────────────────

    Private Sub BoucleSimulation()
        Dim sw As New System.Diagnostics.Stopwatch()
        Do While Not _annuler
            sw.Restart()
            Try
                Dim t = DateTime.Now
                If Gestionnaire IsNot Nothing Then
                    For Each c In Gestionnaire.Centrales
                        SimulerCentrale(c, t)
                        NombreMesures += 1
                        If GestCalculs IsNot Nothing AndAlso Historique IsNot Nothing Then
                            Dim dtSec = IntervalleMsec / 1000.0
                            Historique.AjouterMesuresCentrale(c, t)
                            GestCalculs.CalculerEtInjecter(Historique, t, dtSec)
                        End If
                        If StockerCSV AndAlso _csvInit Then EcrireCSV(c, t)
                        If FormulairePrincipal IsNot Nothing AndAlso Not FormulairePrincipal.IsDisposed Then
                            Dim cc = c
                            Dim tt = t
                            FormulairePrincipal.BeginInvoke(Sub() RaiseEvent NouvellesMesures(Me, cc, tt))
                        End If
                    Next
                End If
            Catch ex As Exception
                If Not _annuler Then RaiseEvent ErreurAcquisition(Me, "Simulation : " & ex.Message)
            End Try
            Dim reste = CInt(IntervalleMsec - sw.ElapsedMilliseconds)
            If reste > 0 Then Thread.Sleep(reste)
        Loop
        FermerCSV()
        EnCours = False
    End Sub

    Private Sub SimulerCentrale(c As CentraleKeithley, t As DateTime)
        Dim sec      = (t - _simDepart).TotalSeconds
        Dim phase    = (sec Mod 7200.0) / 7200.0
        Dim decalage = (c.Numero - 1) * 0.25
        Dim baseT    = 35.0 + 40.0 * Math.Sin((phase + decalage) * Math.PI)

        ' ── Voies de mesure ──
        Dim tok As New List(Of String)
        Dim idx As Integer = 0
        For Each v In c.Voies.Voies.Where(Function(x) x.Active)
            Dim val As Double
            If v.Type = VoieMesure.TypeVoie.Temperature Then
                val = baseT - 2.0 * idx + Gauss() * 0.4
            Else
                Dim refD = (If(phase < 0.5, 0.8, 0.3)) * (v.DebitMax - v.DebitMin) + v.DebitMin
                Dim d    = Math.Max(0, refD + Gauss() * 2.0)
                Dim r    = If(v.DebitMax - v.DebitMin > 0, (d - v.DebitMin) / (v.DebitMax - v.DebitMin), 0)
                val = 1.0 + Math.Max(0.0, Math.Min(1.0, r)) * 4.0
            End If
            tok.Add(val.ToString("E6", System.Globalization.CultureInfo.InvariantCulture))
            idx += 1
        Next
        If tok.Count > 0 Then c.Voies.ParseReponse(String.Join(",", tok), t)

        ' ── Sorties simulées : alternance simple pour toutes, peu importe le nom ──
        Dim cycleONOFF = (CInt(sec) Mod 10) < 7   ' 7s ON, 3s OFF
        For Each s In c.Voies.SortiesActives()
            s.TensionV = If(cycleONOFF, s.UMax, 0.0)
        Next
    End Sub

    Private Function Gauss() As Double
        Dim u1 = 1.0 - _rng.NextDouble()
        Dim u2 = 1.0 - _rng.NextDouble()
        Return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2)
    End Function

    ' ─── Contexte chronogramme (optionnel, écrit en commentaire dans le CSV) ──

    ''' <summary>Contexte du chronogramme lancé en parallèle (None si pas de chrono).</summary>
    Public Property ContexteChronogramme As ContexteCSVChronogramme = Nothing

    ' ─── CSV ──────────────────────────────────────────────────────────────────

    Private Function InitCSV() As Boolean
        Try
            If CheminCSV = "" Then
                CheminCSV = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Thermopilot",
                    "Mesures_" & DateTime.Now.ToString("yyyyMMdd-HH.mm.ss") & ".csv")
            End If
            Directory.CreateDirectory(Path.GetDirectoryName(CheminCSV))
            _writerCSV = New StreamWriter(CheminCSV, False, System.Text.Encoding.UTF8)

            ' En-tête des colonnes (pas de commentaires # — CSV commence directement)
            If Gestionnaire IsNot Nothing Then
                _writerCSV.WriteLine(Gestionnaire.EnteteCSV())
            End If
            _writerCSV.Flush()
            _csvInit = True
            Return True
        Catch ex As Exception
            RaiseEvent ErreurAcquisition(Me, "CSV init : " & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Écrit une ligne CSV incluant les données de TOUTES les centrales
    ''' à l'horodatage courant (les voies non mises à jour gardent leur dernière valeur).
    ''' Thread-safe.
    ''' </summary>
    Private Sub EcrireCSV(centrale As CentraleKeithley, horodatage As DateTime)
        Try
            SyncLock _lockCSV
                If _writerCSV IsNot Nothing AndAlso Gestionnaire IsNot Nothing Then
                    ' Consommer la notification en attente (vide = "" = colonne vide)
                    Dim notif As String
                    SyncLock _lockNotif
                        notif = _notificationEnAttente
                        _notificationEnAttente = ""
                    End SyncLock
                    _writerCSV.WriteLine(Gestionnaire.LigneCSV(horodatage, notif))
                    _writerCSV.Flush()
                End If
            End SyncLock
        Catch ex As Exception
            RaiseEvent ErreurAcquisition(Me, "CSV écriture : " & ex.Message)
        End Try
    End Sub

    Private Sub FermerCSV()
        Try
            SyncLock _lockCSV
                If _writerCSV IsNot Nothing Then
                    _writerCSV.Close()
                    _writerCSV.Dispose()
                    _writerCSV = Nothing
                End If
            End SyncLock
            _csvInit = False
        Catch
        End Try
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  HISTORIQUE MULTI-CENTRALE
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Historique unifié pour toutes les centrales.
''' Clé = (NumeroCentrale, NumeroVoie) pour les voies
''' Clé = (NumeroCentrale, -NumeroSortie) pour les sorties
''' </summary>
Public Class HistoriqueMultiCentrale

    Private _lock      As New Object()
    Private _maxPoints As Integer
    Private _donnees   As New Dictionary(Of String, Queue(Of PointMesure))
    Private _horodatages As New Queue(Of DateTime)

    Public Sub New(maxPoints As Integer)
        _maxPoints = maxPoints
    End Sub

    ''' <summary>Clé unique pour une voie : "C{centrale}_V{voie}"</summary>
    Public Shared Function CleVoie(numeroCentrale As Integer, numeroVoie As Integer) As String
        Return "C" & numeroCentrale & "_V" & numeroVoie
    End Function

    ''' <summary>Clé unique pour une sortie : "C{centrale}_S{sortie}"</summary>
    Public Shared Function CleSortie(numeroCentrale As Integer, numeroSortie As Integer) As String
        Return "C" & numeroCentrale & "_S" & numeroSortie
    End Function

    ''' <summary>
    ''' Injection directe d'un point par clé — utilisé par OngletVisuCSV
    ''' pour rejouer un fichier CSV sans passer par une CentraleKeithley.
    ''' </summary>
    Public Sub InjecterPoint(cle As String, pt As PointMesure)
        SyncLock _lock
            If Not _donnees.ContainsKey(cle) Then
                _donnees(cle) = New Queue(Of PointMesure)
            End If
            _donnees(cle).Enqueue(pt)
            If _donnees(cle).Count > _maxPoints Then _donnees(cle).Dequeue()
        End SyncLock
    End Sub

    ''' <summary>
    ''' Ajoute un horodatage global — utilisé conjointement avec InjecterPoint.
    ''' </summary>
    Public Sub AjouterHorodatage(dt As DateTime)
        SyncLock _lock
            _horodatages.Enqueue(dt)
            If _horodatages.Count > _maxPoints Then _horodatages.Dequeue()
        End SyncLock
    End Sub

    Public Sub AjouterMesuresCentrale(centrale As CentraleKeithley, horodatage As DateTime)
        SyncLock _lock
            ' Horodatage global (on prend le premier tick reçu par cycle)
            _horodatages.Enqueue(horodatage)
            If _horodatages.Count > _maxPoints Then _horodatages.Dequeue()

            ' Voies
            For Each v In centrale.Voies.Voies.Where(Function(x) x.Active)
                Dim cle = CleVoie(centrale.Numero, v.Numero)
                If Not _donnees.ContainsKey(cle) Then
                    _donnees(cle) = New Queue(Of PointMesure)
                End If
                _donnees(cle).Enqueue(New PointMesure With {
                    .Horodatage       = horodatage,
                    .Valeur           = v.Valeur,
                    .ValeurGraphiqueB = Double.NaN,
                    .EnErreur         = v.EnErreur,
                    .EnAlarme         = v.EnAlarme
                })
                If _donnees(cle).Count > _maxPoints Then _donnees(cle).Dequeue()
            Next

            ' Sorties
            For Each s In centrale.Voies.SortiesActives()
                Dim cle = CleSortie(centrale.Numero, s.Numero)
                If Not _donnees.ContainsKey(cle) Then
                    _donnees(cle) = New Queue(Of PointMesure)
                End If
                _donnees(cle).Enqueue(New PointMesure With {
                    .Horodatage       = horodatage,
                    .Valeur           = s.TensionV,
                    .ValeurGraphiqueB = s.ValeurGraphique,
                    .EnErreur         = False,
                    .EnAlarme         = False
                })
                If _donnees(cle).Count > _maxPoints Then _donnees(cle).Dequeue()
            Next
        End SyncLock
    End Sub

    Public Function ObtenirSerie(cle As String) As List(Of PointMesure)
        SyncLock _lock
            Return If(_donnees.ContainsKey(cle),
                      New List(Of PointMesure)(_donnees(cle)),
                      New List(Of PointMesure)())
        End SyncLock
    End Function

    Public Function ObtenirHorodatages() As List(Of DateTime)
        SyncLock _lock
            Return New List(Of DateTime)(_horodatages)
        End SyncLock
    End Function

    Public Sub Vider()
        SyncLock _lock
            _donnees.Clear()
            _horodatages.Clear()
        End SyncLock
    End Sub

End Class

Public Class PointMesure
    Public Property Horodatage       As DateTime
    Public Property Valeur           As Double
    Public Property ValeurGraphiqueB As Double = Double.NaN
    Public Property EnErreur         As Boolean
    Public Property EnAlarme         As Boolean
End Class
