Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>
''' Panneau repliable affichant les voies de surveillance débit actives,
''' leurs seuils bas/haut et leur valeur courante.
''' À insérer en DockStyle.Top dans les onglets Acquisition, Relais, Chrono, Système.
''' Appeler MettreAJour() à chaque nouvelle mesure.
''' </summary>
Public Class PanneauInfoDebit

    ' ─── Contrôles ────────────────────────────────────────────────────────────

    Private _pnlEntete  As New Panel()
    Private _pnlCorps   As New FlowLayoutPanel()
    Private _btnToggle  As New Button()
    Private _lblResume  As New Label()
    Private _ouvert     As Boolean = False
    Private _conteneur  As Panel   ' le Panel racine retourné à l'appelant

    ' ─── Données ─────────────────────────────────────────────────────────────

    Private _gestionnaire As GestionnaireMultiCentrale = Nothing

    ' ─── Construction ─────────────────────────────────────────────────────────

    Public Function ConstruirePanel(gestionnaire As GestionnaireMultiCentrale) As Control
        _gestionnaire = gestionnaire

        _conteneur              = New Panel()
        _conteneur.Dock         = DockStyle.Top
        _conteneur.BackColor    = Color.FromArgb(235, 242, 255)
        _conteneur.AutoSize     = True
        _conteneur.AutoSizeMode = AutoSizeMode.GrowAndShrink

        ' ── En-tête (toujours visible) ──
        _pnlEntete.Dock      = DockStyle.Top
        _pnlEntete.Height    = 24
        _pnlEntete.BackColor = Color.FromArgb(200, 218, 248)
        _pnlEntete.Cursor    = Cursors.Hand

        _btnToggle.Text      = "▶"
        _btnToggle.FlatStyle = FlatStyle.Flat
        _btnToggle.FlatAppearance.BorderSize = 0
        _btnToggle.BackColor = Color.Transparent
        _btnToggle.ForeColor = Color.FromArgb(40, 70, 140)
        _btnToggle.Font      = New Font("Segoe UI", 8, FontStyle.Bold)
        _btnToggle.Size      = New Size(22, 22)
        _btnToggle.Location  = New Point(4, 1)
        _btnToggle.Cursor    = Cursors.Hand

        Dim lblTitre As New Label() With {
            .Text      = "SURVEILLANCE SÉCURITÉ",
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 70, 140),
            .AutoSize  = True,
            .Location  = New Point(28, 5)
        }

        _lblResume.AutoSize  = True
        _lblResume.Font      = New Font("Segoe UI", 8, FontStyle.Italic)
        _lblResume.ForeColor = Color.FromArgb(80, 100, 140)
        _lblResume.Location  = New Point(170, 5)

        _pnlEntete.Controls.Add(_btnToggle)
        _pnlEntete.Controls.Add(lblTitre)
        _pnlEntete.Controls.Add(_lblResume)

        ' ── Corps (repliable) ──
        _pnlCorps.Dock          = DockStyle.Top
        _pnlCorps.AutoSize      = True
        _pnlCorps.AutoSizeMode  = AutoSizeMode.GrowAndShrink
        _pnlCorps.FlowDirection = FlowDirection.TopDown
        _pnlCorps.WrapContents  = False
        _pnlCorps.Padding       = New Padding(8, 4, 8, 4)
        _pnlCorps.Visible       = False

        _conteneur.Controls.Add(_pnlCorps)
        _conteneur.Controls.Add(_pnlEntete)

        AddHandler _btnToggle.Click,    AddressOf Toggle_Click
        AddHandler _pnlEntete.Click,    AddressOf Toggle_Click

        ActualiserContenu()
        Return _conteneur
    End Function

    ' ─── Toggle repli/dépli ───────────────────────────────────────────────────

    Private Sub Toggle_Click(sender As Object, e As EventArgs)
        _ouvert          = Not _ouvert
        _pnlCorps.Visible = _ouvert
        _btnToggle.Text   = If(_ouvert, "▼", "▶")
    End Sub

    ' ─── Mise à jour du contenu ───────────────────────────────────────────────

    Public Sub MettreAJour()
        ' Mise à jour des valeurs uniquement (pas reconstruction)
        If _gestionnaire Is Nothing Then Return
        Dim voiesSurv = ObtenirVoiesSurveillees()

        If voiesSurv.Count = 0 Then
            _lblResume.Text      = "Aucune voie de surveillance configurée"
            _lblResume.ForeColor = Color.Gray
        Else
            ' Vérifier si l'une est hors plage
            Dim nbHorsPlage = voiesSurv.Where(Function(v) EstHorsPlage(v)).Count()
            If nbHorsPlage > 0 Then
                _lblResume.Text      = String.Format("⚠  {0} voie(s) hors plage !", nbHorsPlage)
                _lblResume.ForeColor = Color.DarkRed
            Else
                _lblResume.Text      = String.Format("✔  {0} voie(s) — OK", voiesSurv.Count)
                _lblResume.ForeColor = Color.FromArgb(30, 130, 50)
            End If
        End If

        ' Mettre à jour les labels dans le corps
        For Each ctrl In _pnlCorps.Controls.OfType(Of LigneDebit)()
            ctrl.Rafraichir()
        Next
    End Sub

    ''' <summary>Reconstruit entièrement le contenu (appeler après OnVoiesAppliquees).</summary>
    Public Sub ActualiserContenu()
        _pnlCorps.Controls.Clear()
        If _gestionnaire Is Nothing Then Return

        Dim voiesSurv = ObtenirVoiesSurveillees()

        If voiesSurv.Count = 0 Then
            _pnlCorps.Controls.Add(New Label() With {
                .Text      = "Aucune voie n'est marquée « Surveill. sécu. » dans les onglets Centrale.",
                .AutoSize  = True,
                .Font      = New Font("Segoe UI", 8.5, FontStyle.Italic),
                .ForeColor = Color.Gray,
                .Margin    = New Padding(0, 2, 0, 2)
            })
            _lblResume.Text      = "Non configuré"
            _lblResume.ForeColor = Color.Gray
        Else
            ' En-tête des colonnes
            Dim entete As New LigneDebit(Nothing, Nothing, True)
            _pnlCorps.Controls.Add(entete)

            For Each item In voiesSurv
                _pnlCorps.Controls.Add(New LigneDebit(item.Voie, item.NomCentrale, False))
            Next
        End If
        MettreAJour()
    End Sub

    ' ─── Helpers ──────────────────────────────────────────────────────────────

    Private Function ObtenirVoiesSurveillees() As List(Of (Voie As VoieMesure, NomCentrale As String))
        Dim result As New List(Of (VoieMesure, String))
        If _gestionnaire Is Nothing Then Return result
        For Each c In _gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active AndAlso x.SurveillanceDebit)
                result.Add((v, c.NomAffiche))
            Next
        Next
        Return result
    End Function

    Private Shared Function EstHorsPlage(item As (Voie As VoieMesure, NomCentrale As String)) As Boolean
        Dim v = item.Voie
        If Double.IsNaN(v.Valeur) OrElse v.EnErreur Then Return False
        If Not Double.IsNaN(v.SeuilBas)  AndAlso v.Valeur < v.SeuilBas  Then Return True
        If Not Double.IsNaN(v.SeuilHaut) AndAlso v.Valeur > v.SeuilHaut Then Return True
        Return False
    End Function

    ' ─── Ligne d'affichage ────────────────────────────────────────────────────

    Private Class LigneDebit
        Inherits FlowLayoutPanel

        Private _voie        As VoieMesure
        Private _nomCentrale As String
        Private _estEntete   As Boolean
        Private _lblValeur   As New Label()
        Private _lblEtat     As New Label()

        Public Sub New(voie As VoieMesure, nomCentrale As String, estEntete As Boolean)
            _voie        = voie
            _nomCentrale = nomCentrale
            _estEntete   = estEntete

            Me.AutoSize      = True
            Me.FlowDirection = FlowDirection.LeftToRight
            Me.WrapContents  = False
            Me.Margin        = New Padding(0, 1, 0, 1)

            If estEntete Then
                Me.BackColor = Color.FromArgb(215, 225, 248)
                CrLbl("Centrale",       70,  FontStyle.Bold)
                CrLbl("Voie",           55,  FontStyle.Bold)
                CrLbl("Nom",            160, FontStyle.Bold)
                CrLbl("Unité",          55,  FontStyle.Bold)
                CrLbl("Seuil bas",      75,  FontStyle.Bold)
                CrLbl("Seuil haut",     75,  FontStyle.Bold)
                CrLbl("Valeur actuelle",100, FontStyle.Bold)
                CrLbl("État",           70,  FontStyle.Bold)
            Else
                Me.BackColor = Color.White
                CrLbl(nomCentrale,                    70)
                CrLbl(voie.Numero.ToString(),         55)
                CrLbl(voie.Nom,                       160)
                CrLbl(voie.Unite,                     55)
                CrLbl(FormatSeuil(voie.SeuilBas),     75)
                CrLbl(FormatSeuil(voie.SeuilHaut),    75)
                _lblValeur.Width      = 100
                _lblValeur.AutoSize   = False
                _lblValeur.Font       = New Font("Consolas", 9, FontStyle.Bold)
                _lblValeur.TextAlign  = ContentAlignment.MiddleRight
                Me.Controls.Add(_lblValeur)
                _lblEtat.Width     = 70
                _lblEtat.AutoSize  = False
                _lblEtat.Font      = New Font("Segoe UI", 8)
                _lblEtat.TextAlign = ContentAlignment.MiddleCenter
                Me.Controls.Add(_lblEtat)
                Rafraichir()
            End If
        End Sub

        Public Sub Rafraichir()
            If _estEntete OrElse _voie Is Nothing Then Return
            Dim val = _voie.Valeur
            If Double.IsNaN(val) OrElse _voie.EnErreur Then
                _lblValeur.Text      = "ERR"
                _lblValeur.ForeColor = Color.Orange
                _lblEtat.Text        = "—"
                _lblEtat.ForeColor   = Color.Gray
                Me.BackColor         = Color.White
            Else
                _lblValeur.Text      = val.ToString("F3")
                Dim horsPlage = False
                If Not Double.IsNaN(_voie.SeuilBas)  AndAlso val < _voie.SeuilBas  Then horsPlage = True
                If Not Double.IsNaN(_voie.SeuilHaut) AndAlso val > _voie.SeuilHaut Then horsPlage = True
                If horsPlage Then
                    _lblValeur.ForeColor = Color.DarkRed
                    _lblEtat.Text        = "⚠ HORS PLAGE"
                    _lblEtat.ForeColor   = Color.DarkRed
                    Me.BackColor         = Color.FromArgb(255, 235, 235)
                Else
                    _lblValeur.ForeColor = Color.FromArgb(20, 120, 40)
                    _lblEtat.Text        = "✔ OK"
                    _lblEtat.ForeColor   = Color.FromArgb(20, 120, 40)
                    Me.BackColor         = Color.White
                End If
            End If
        End Sub

        Private Sub CrLbl(texte As String, largeur As Integer,
                          Optional style As FontStyle = FontStyle.Regular)
            Dim lbl As New Label() With {
                .Text      = texte,
                .Width     = largeur,
                .AutoSize  = False,
                .Font      = New Font("Segoe UI", 8.5, style),
                .ForeColor = If(_estEntete, Color.FromArgb(40, 60, 120), Color.Black),
                .TextAlign = ContentAlignment.MiddleLeft,
                .Margin    = New Padding(2, 0, 2, 0)
            }
            Me.Controls.Add(lbl)
        End Sub

        Private Shared Function FormatSeuil(v As Double) As String
            Return If(Double.IsNaN(v), "—", v.ToString("F2"))
        End Function
    End Class

End Class
