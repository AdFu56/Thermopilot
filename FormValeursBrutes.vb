Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

''' <summary>
''' Fenêtre de diagnostic : affiche la valeur brute (telle que retournée par le Keithley)
''' de chaque voie active de la centrale, sans aucune conversion ni traitement.
''' Permet de vérifier le bon câblage et le bon fonctionnement des capteurs.
''' Accessible via le bouton "Valeurs brutes" dans l'onglet Centrale X.
''' </summary>
Public Class FormValeursBrutes
    Inherits Form

    ' ─── Références ───────────────────────────────────────────────────────────

    Private ReadOnly _centrale        As CentraleKeithley
    Private ReadOnly _gestionnaire    As GestionnaireMultiCentrale

    ' ─── Contrôles ────────────────────────────────────────────────────────────

    Private _dgv            As New DataGridView()
    Private _btnRafraichir  As New Button()
    Private _btnFermer      As New Button()
    Private _chkAuto        As New CheckBox()
    Private _numIntervalle  As New NumericUpDown()
    Private _lblStatut      As New Label()
    Private _timer          As New System.Windows.Forms.Timer()
    Private _lblDerniereScan As New Label()

    ' ─── Constructeur ─────────────────────────────────────────────────────────

    ''' <summary>
    ''' Affiche les valeurs brutes d'une centrale spécifique.
    ''' </summary>
    Public Sub New(centrale As CentraleKeithley,
                   Optional gestionnaire As GestionnaireMultiCentrale = Nothing)
        _centrale     = centrale
        _gestionnaire = gestionnaire

        Me.Text          = "Valeurs brutes — " & centrale.NomAffiche
        Me.Size          = New Size(700, 520)
        Me.MinimumSize   = New Size(500, 350)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Font          = New Font("Segoe UI", 9)
        Me.BackColor     = Color.FromArgb(24, 26, 34)

        ConstruireUI()
        Rafraichir()
    End Sub

    ' ─── Construction de l'interface ──────────────────────────────────────────

    Private Sub ConstruireUI()
        ' ── Barre d'outils ──
        Dim pnlTop As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Top,
            .Height        = 46,
            .Padding       = New Padding(8, 8, 8, 0),
            .FlowDirection = FlowDirection.LeftToRight,
            .BackColor     = Color.FromArgb(30, 33, 44)
        }

        _btnRafraichir.Text      = "↺ Rafraîchir"
        _btnRafraichir.BackColor = Color.FromArgb(40, 110, 175)
        _btnRafraichir.ForeColor = Color.White
        _btnRafraichir.FlatStyle = FlatStyle.Flat
        _btnRafraichir.Height    = 28
        _btnRafraichir.AutoSize  = True

        _chkAuto.Text      = "Auto :"
        _chkAuto.ForeColor = Color.FromArgb(180, 190, 210)
        _chkAuto.AutoSize  = True
        _chkAuto.Margin    = New Padding(12, 8, 4, 0)

        _numIntervalle.Minimum  = 1
        _numIntervalle.Maximum  = 60
        _numIntervalle.Value    = 5
        _numIntervalle.Width    = 55
        _numIntervalle.Font     = New Font("Consolas", 9)
        _numIntervalle.Margin   = New Padding(0, 3, 0, 0)

        Dim lblSec As New Label() With {
            .Text      = "s",
            .ForeColor = Color.FromArgb(150, 160, 180),
            .AutoSize  = True,
            .Margin    = New Padding(3, 8, 12, 0)
        }

        _btnFermer.Text      = "✕ Fermer"
        _btnFermer.BackColor = Color.FromArgb(70, 35, 35)
        _btnFermer.ForeColor = Color.White
        _btnFermer.FlatStyle = FlatStyle.Flat
        _btnFermer.Height    = 28
        _btnFermer.AutoSize  = True
        _btnFermer.Margin    = New Padding(8, 0, 0, 0)

        pnlTop.Controls.AddRange({_btnRafraichir, _chkAuto, _numIntervalle, lblSec, _btnFermer})

        ' ── Grille ──
        _dgv.Dock                  = DockStyle.Fill
        _dgv.AllowUserToAddRows    = False
        _dgv.AllowUserToDeleteRows = False
        _dgv.RowHeadersVisible     = False
        _dgv.ReadOnly              = True
        _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgv.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgv.Font                  = New Font("Consolas", 9)
        _dgv.BackgroundColor       = Color.FromArgb(18, 20, 28)
        _dgv.GridColor             = Color.FromArgb(40, 45, 60)
        _dgv.BorderStyle           = BorderStyle.None
        _dgv.CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal
        _dgv.DefaultCellStyle.BackColor   = Color.FromArgb(22, 25, 35)
        _dgv.DefaultCellStyle.ForeColor   = Color.FromArgb(200, 215, 240)
        _dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 80, 130)
        _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 40, 58)
        _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(140, 160, 200)
        _dgv.ColumnHeadersDefaultCellStyle.Font      = New Font("Segoe UI", 8.5, FontStyle.Bold)
        _dgv.EnableHeadersVisualStyles = False

        _dgv.Columns.AddRange({
            New DataGridViewTextBoxColumn() With {.Name = "cVoie",   .HeaderText = "N° voie",      .Width = 80},
            New DataGridViewTextBoxColumn() With {.Name = "cNom",    .HeaderText = "Nom",           .Width = 180},
            New DataGridViewTextBoxColumn() With {.Name = "cType",   .HeaderText = "Type",          .Width = 100},
            New DataGridViewTextBoxColumn() With {.Name = "cBrut",   .HeaderText = "Valeur brute",  .Width = 130},
            New DataGridViewTextBoxColumn() With {.Name = "cConv",   .HeaderText = "Valeur conv.",  .Width = 110},
            New DataGridViewTextBoxColumn() With {.Name = "cUnite",  .HeaderText = "Unité",         .Width = 70},
            New DataGridViewTextBoxColumn() With {.Name = "cEtat",   .HeaderText = "État",          .Width = 100}
        })

        ' ── Barre de statut ──
        Dim pnlBas As New Panel() With {
            .Dock      = DockStyle.Bottom,
            .Height    = 28,
            .BackColor = Color.FromArgb(30, 33, 44)
        }
        _lblStatut.AutoSize  = True
        _lblStatut.ForeColor = Color.FromArgb(100, 120, 160)
        _lblStatut.Font      = New Font("Segoe UI", 8)
        _lblStatut.Location  = New Point(8, 7)
        _lblStatut.Text      = "Cliquez sur Rafraîchir ou activez le mode automatique."

        _lblDerniereScan.AutoSize  = True
        _lblDerniereScan.ForeColor = Color.FromArgb(80, 100, 140)
        _lblDerniereScan.Font      = New Font("Segoe UI", 8)
        _lblDerniereScan.Anchor    = AnchorStyles.Right Or AnchorStyles.Top
        _lblDerniereScan.Location  = New Point(500, 7)

        pnlBas.Controls.Add(_lblStatut)
        pnlBas.Controls.Add(_lblDerniereScan)

        Me.Controls.Add(_dgv)
        Me.Controls.Add(pnlBas)
        Me.Controls.Add(pnlTop)

        ' ── Timer auto-refresh ──
        _timer.Interval = 5000
        AddHandler _timer.Tick,            Sub(s, e) Rafraichir()
        AddHandler _btnRafraichir.Click,   Sub(s, e) Rafraichir()
        AddHandler _btnFermer.Click,       Sub(s, e) Me.Close()
        AddHandler _chkAuto.CheckedChanged, AddressOf ChkAuto_Changed
        AddHandler _numIntervalle.ValueChanged, Sub(s, e)
            _timer.Interval = CInt(_numIntervalle.Value) * 1000
        End Sub
        AddHandler Me.FormClosing, Sub(s, e) _timer.Stop()
    End Sub

    ' ─── Rafraîchissement ─────────────────────────────────────────────────────

    Private Sub Rafraichir()
        _dgv.Rows.Clear()

        If _centrale Is Nothing Then
            _lblStatut.Text = "Centrale non disponible."
            Return
        End If

        ' Si la centrale est connectée mais pas en acquisition → lire maintenant
        If _centrale.EstConnectee AndAlso Not _centrale.EnAcquisition Then
            _lblStatut.Text = "Lecture en cours…"
            Me.Cursor = Cursors.WaitCursor
            Application.DoEvents()
            Try
                Task.Run(Sub()
                    _centrale.LireMesureInstantanee()
                    Me.BeginInvoke(Sub() AfficherValeurs())
                End Sub)
            Catch
                Me.Cursor = Cursors.Default
                AfficherValeurs()
            End Try
            Return
        End If

        AfficherValeurs()
    End Sub

    Private Sub AfficherValeurs()
        Me.Cursor = Cursors.Default
        _dgv.Rows.Clear()

        Dim voies = _centrale.Voies.Voies.Where(Function(v) v.Active).ToList()

        If voies.Count = 0 Then
            _lblStatut.Text = "Aucune voie active. Configurez les voies dans l'onglet Centrale puis cliquez ⚠ APPLIQUER."
            Return
        End If

        Dim nbOK    = 0
        Dim nbErreur = 0

        For Each v In voies
            ' Valeur brute = Valeur interne avant toute transformation d'affichage
            ' Pour les TC : déjà en °C (conversion faite par le Keithley)
            ' Pour les 4-20mA : tension en V sur le shunt
            Dim valBrut As String
            Dim valConv As String
            Dim etat    As String
            Dim coulFond As Color
            Dim coulTexte As Color

            If v.EnErreur Then
                valBrut   = "ERR"
                valConv   = "ERR"
                etat      = "⚠ Erreur"
                coulFond  = Color.FromArgb(60, 25, 25)
                coulTexte = Color.FromArgb(220, 100, 90)
                nbErreur += 1
            ElseIf Double.IsNaN(v.Valeur) Then
                valBrut   = "---"
                valConv   = "---"
                etat      = "En attente"
                coulFond  = Color.FromArgb(22, 25, 35)
                coulTexte = Color.FromArgb(100, 110, 140)
            Else
                ' Valeur brute = valeur telle que lue (°C pour TC, V pour tension)
                valBrut  = v.ValeurBrute.ToString("G6",
                    System.Globalization.CultureInfo.InvariantCulture)
                ' Valeur convertie = après application du périphérique (L/h, bar, etc.)
                valConv  = v.Valeur.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture)
                etat     = "✔ OK"
                coulFond  = Color.FromArgb(20, 35, 28)
                coulTexte = Color.FromArgb(100, 210, 140)
                nbOK     += 1

                ' Signaler valeur hors plage alarme en orange
                If v.AlarmeActive AndAlso (v.EnAlarmeHaute OrElse v.EnAlarmeBasse) Then
                    etat     = "⚠ ALARME"
                    coulFond  = Color.FromArgb(55, 45, 20)
                    coulTexte = Color.FromArgb(220, 170, 60)
                End If

                ' Signaler 9.9E+37 (entrée ouverte / overrange Keithley)
                If Math.Abs(v.ValeurBrute) > 9.0E+36 Then
                    valBrut   = "9.9E+37"
                    etat      = "Entrée ouverte"
                    coulFond  = Color.FromArgb(50, 40, 20)
                    coulTexte = Color.FromArgb(200, 160, 60)
                End If
            End If

            ' Type de voie
            Dim typeStr As String
            Select Case v.Type
                Case VoieMesure.TypeVoie.Temperature : typeStr = "TC / Temp."
                Case VoieMesure.TypeVoie.Debit       : typeStr = "4-20mA / Débit"
                Case Else                             : typeStr = "Inconnu"
            End Select

            Dim idx = _dgv.Rows.Add(
                v.Numero.ToString(),
                v.Nom,
                typeStr,
                valBrut,
                valConv,
                v.Unite,
                etat)

            _dgv.Rows(idx).DefaultCellStyle.BackColor = coulFond
            _dgv.Rows(idx).Cells("cEtat").Style.ForeColor = coulTexte
            _dgv.Rows(idx).Cells("cBrut").Style.ForeColor = Color.FromArgb(160, 200, 255)
        Next

        ' Ajouter les sorties analogiques
        Dim sorties = _centrale.Voies.SortiesActives().ToList()
        If sorties.Count > 0 Then
            Dim idxSep = _dgv.Rows.Add("─── Sorties analogiques ───", "", "", "", "", "", "")
            _dgv.Rows(idxSep).DefaultCellStyle.BackColor = Color.FromArgb(35, 38, 52)
            _dgv.Rows(idxSep).DefaultCellStyle.ForeColor = Color.FromArgb(100, 120, 170)
            _dgv.Rows(idxSep).DefaultCellStyle.Font      = New Font("Segoe UI", 8, FontStyle.Bold)

            For Each s In sorties
                Dim etatSortie = If(s.EstOn, "ON  → " & s.TensionV.ToString("F2") & " V", "OFF (0 V)")
                Dim coulSortie = If(s.EstOn, Color.FromArgb(25, 50, 35), Color.FromArgb(22, 25, 35))
                Dim coulEtat   = If(s.EstOn, Color.FromArgb(80, 200, 120), Color.FromArgb(100, 110, 140))
                Dim idx = _dgv.Rows.Add(
                    s.Numero.ToString(),
                    s.Nom,
                    If(s.Mode = SortieAnalogique.ModePilotage.Analogique OrElse
                       s.Mode = SortieAnalogique.ModePilotage.AnalogiqueFull,
                       "Analogique" & If(s.Mode = SortieAnalogique.ModePilotage.AnalogiqueFull, " full", ""),
                       "Booléen"),
                    s.TensionV.ToString("F3") & " V",
                    s.TensionV.ToString("F3"),
                    "V",
                    etatSortie)
                _dgv.Rows(idx).DefaultCellStyle.BackColor     = coulSortie
                _dgv.Rows(idx).Cells("cEtat").Style.ForeColor = coulEtat
            Next
        End If

        ' Statut
        _lblStatut.Text = String.Format(
            "{0} voie(s) active(s) — {1} OK, {2} en erreur",
            voies.Count, nbOK, nbErreur)
        _lblDerniereScan.Text = "Dernière lecture : " & DateTime.Now.ToString("HH:mm:ss")

        ' Couleur statut
        _lblStatut.ForeColor = If(nbErreur > 0,
            Color.FromArgb(210, 120, 80),
            Color.FromArgb(80, 180, 120))
    End Sub

    Private Sub ChkAuto_Changed(sender As Object, e As EventArgs)
        If _chkAuto.Checked Then
            _timer.Interval = CInt(_numIntervalle.Value) * 1000
            _timer.Start()
            _lblStatut.ForeColor = Color.FromArgb(80, 160, 220)
            _lblStatut.Text      = "Rafraîchissement automatique actif."
        Else
            _timer.Stop()
        End If
    End Sub

End Class
