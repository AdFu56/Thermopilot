Imports System.Windows.Forms
Imports System.Drawing
Imports System.Threading

Imports System
Imports System.Collections.Generic ' <--- Résout l'erreur Dictionary et List
Imports System.IO                  ' <--- Résout l'erreur IO
Imports System.Linq                ' <--- Résout l'erreur sur les DataGridView

''' <summary>
''' Moteur d'alarmes : reçoit les notifications de GestionVoies.AlarmeChangee,
''' déclenche les actions configurées :
'''   - Clignotement visuel de la ligne dans le DataGridView (rouge/rose, 500 ms)
'''   - Message en barre de statut
'''   - Bip sonore système (880 Hz, 200 ms)
'''   - Coupure automatique du relais associé à la voie
''' </summary>
Public Class MoteurAlarmes

    ' ─── Références externes ──────────────────────────────────────────────────

    Public Property GestionVoies As GestionVoies
    Public Property Keithley     As KeithleyComm
    Public Property Relais       As Dictionary(Of Relais.NomRelais, Relais)
    Public Property DgvMesures   As DataGridView
    Public Property FormUI       As Form

    ' ─── Configuration ────────────────────────────────────────────────────────

    Public Property BipActif          As Boolean = True
    Public Property CoupureRelaisActif As Boolean = True

    ' ─── État interne ─────────────────────────────────────────────────────────

    ' Clignottement : timer qui inverse la couleur des lignes en alarme
    Private WithEvents _timerCligno As New System.Windows.Forms.Timer() With {.Interval = 500}
    Private _clignoEtat As Boolean = False

    ' Ensemble des voies actuellement en alarme (pour le clignotement)
    Private _voiesEnAlarme As New HashSet(Of Integer)   ' numéros de voie

    ' ─── Événements publics ───────────────────────────────────────────────────

    ''' <summary>Déclenché sur le thread UI avec le message à afficher en statut.</summary>
    Public Event AlarmeStatut(sender As Object, message As String, estAlarme As Boolean)

    ''' <summary>Déclenché quand un relais est coupé par sécurité alarme.</summary>
    Public Event RelaisCoupeParAlarme(sender As Object, nomRelais As String, nomVoie As String)

    ' ─── Initialisation ───────────────────────────────────────────────────────

    Public Sub Demarrer()
        _timerCligno.Start()
    End Sub

    Public Sub Arreter()
        _timerCligno.Stop()
        _voiesEnAlarme.Clear()
    End Sub

    ' ─── Traitement d'une alarme ──────────────────────────────────────────────

    ''' <summary>
    ''' Appelé par GestionVoies.AlarmeChangee (peut venir d'un thread non-UI).
    ''' </summary>
    Public Sub TraiterAlarme(voie As VoieMesure, enAlarme As Boolean)
        If FormUI IsNot Nothing AndAlso Not FormUI.IsDisposed Then
            FormUI.BeginInvoke(Sub() TraiterAlarmeSurUI(voie, enAlarme))
        End If
    End Sub

    Private Sub TraiterAlarmeSurUI(voie As VoieMesure, enAlarme As Boolean)
        If enAlarme Then
            _voiesEnAlarme.Add(voie.Numero)

            ' ── Bip système ──
            If BipActif Then
                Threading.Tasks.Task.Run(Sub() Console.Beep(880, 200))
            End If

            ' ── Coupure relais associé ──
            If CoupureRelaisActif Then
                CoupurRelaisAlarme(voie)
            End If

            ' ── Statut ──
            RaiseEvent AlarmeStatut(Me, voie.MessageAlarme, True)

        Else
            _voiesEnAlarme.Remove(voie.Numero)
            If _voiesEnAlarme.Count = 0 Then
                RaiseEvent AlarmeStatut(Me, "Alarmes résolues.", False)
                ' Restaurer la couleur normale dans la grille
                RestaurerrCouleurGrille()
            End If
        End If
    End Sub

    Private Sub CoupurRelaisAlarme(voie As VoieMesure)
        If Keithley Is Nothing OrElse Not Keithley.IsConnected Then Return
        If Relais Is Nothing Then Return

        Dim nomRelais As Nullable(Of Relais.NomRelais) = Nothing

        If voie.EnAlarmeHaute AndAlso voie.RelaisAlarmeHaut.HasValue Then
            nomRelais = voie.RelaisAlarmeHaut
        ElseIf voie.EnAlarmeBasse AndAlso voie.RelaisAlarmeBas.HasValue Then
            nomRelais = voie.RelaisAlarmeBas
        End If

        If nomRelais.HasValue AndAlso Relais.ContainsKey(nomRelais.Value) Then
            Dim r = Relais(nomRelais.Value)
            If r.Etat Then   ' Ne couper que s'il est allumé
                r.Etat = False
                Keithley.OuvrirRelais(r.VoieKeithley)
                RaiseEvent RelaisCoupeParAlarme(Me, r.NomStr, voie.Nom)
            End If
        End If
    End Sub

    ' ─── Clignotement ────────────────────────────────────────────────────────

    Private Sub _timerCligno_Tick(sender As Object, e As EventArgs) Handles _timerCligno.Tick
        If DgvMesures Is Nothing OrElse DgvMesures.IsDisposed Then Return
        If _voiesEnAlarme.Count = 0 Then Return

        _clignoEtat = Not _clignoEtat

        For Each row As DataGridViewRow In DgvMesures.Rows
            Dim num As Integer
            Dim numStr = If(row.Cells("colVoie") IsNot Nothing AndAlso row.Cells("colVoie").Value IsNot Nothing, row.Cells("colVoie").Value.ToString(), "")
            If Not Integer.TryParse(numStr, num) Then Continue For

            If _voiesEnAlarme.Contains(num) Then
                row.DefaultCellStyle.BackColor = If(_clignoEtat,
                    Color.FromArgb(255, 80, 60),
                    Color.FromArgb(255, 200, 190))
                row.DefaultCellStyle.ForeColor = Color.White
            End If
        Next
    End Sub

    Private Sub RestaurerrCouleurGrille()
        If DgvMesures Is Nothing OrElse DgvMesures.IsDisposed Then Return
        For Each row As DataGridViewRow In DgvMesures.Rows
            row.DefaultCellStyle.BackColor = Color.White
            row.DefaultCellStyle.ForeColor = Color.Black
        Next
    End Sub

    ' ─── Récapitulatif des alarmes actives ───────────────────────────────────

    Public Function ResumeLignes() As String
        If GestionVoies Is Nothing Then Return ""
        Dim alarmes = GestionVoies.VoiesEnAlarme()
        If alarmes.Count = 0 Then Return ""
        Return String.Join("  |  ", alarmes.Select(Function(v) v.MessageAlarme))
    End Function

End Class
