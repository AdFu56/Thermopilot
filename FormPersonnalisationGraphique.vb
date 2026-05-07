Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series

''' <summary>
''' Fenêtre de personnalisation du graphique OxyPlot.
''' Permet de modifier : fond, grille, et pour chaque série :
''' couleur, style de ligne, marqueur, épaisseur.
''' Les préférences sont sauvegardées dans config.ini via StylesGraphique.
''' </summary>
Public Class FormPersonnalisationGraphique
    Inherits Form

    ' ─── Références ───────────────────────────────────────────────────────────

    Private ReadOnly _stylesGraphique As StylesGraphique
    Private ReadOnly _series          As List(Of PanelGraphique.SerieGraphique)
    Private ReadOnly _onAppliquer     As Action   ' callback → redessine le graphique

    ' ─── Contrôles globaux ────────────────────────────────────────────────────

    Private _btnFond            As New Button()
    Private _btnGrille          As New Button()
    Private _btnTexte           As New Button()
    Private _cmbStyleGrille     As New ComboBox()
    Private _numPoliceAxes      As New NumericUpDown()
    Private _numPoliceAxesTitre As New NumericUpDown()
    Private _numMargeBasse      As New NumericUpDown()
    ' Légende
    Private _btnFondLegende         As New Button()
    Private _btnTexteLegende        As New Button()
    Private _btnBordureLegende      As New Button()
    Private _numTaillePoliceLegende As New NumericUpDown()
    Private _cmbPositionLegende     As New ComboBox()
    Private _chkLegendeVisible      As New CheckBox()
    Private _numMargeLegende        As New NumericUpDown()
    Private _numPaddingLegende      As New NumericUpDown()
    Private _cmbPlacementLegende    As New ComboBox()
    ' Grille séries + boutons
    Private _dgv           As New DataGridView()
    Private _btnOK         As New Button()
    Private _btnAnnuler    As New Button()
    Private _btnRAZ        As New Button()

    ' Sauvegarde pour annulation
    Private _sauvegardeAvant As StylesGraphique

    ' ─── Constructeur ─────────────────────────────────────────────────────────

    Public Sub New(stylesGraphique As StylesGraphique,
                   series As List(Of PanelGraphique.SerieGraphique),
                   onAppliquer As Action)
        _stylesGraphique = stylesGraphique
        _series          = series
        _onAppliquer     = onAppliquer
        _sauvegardeAvant = stylesGraphique.Clone()

        Me.Text            = "⚙ Personnalisation du graphique"
        Me.Size            = New Size(820, 620)
        Me.MinimumSize     = New Size(700, 480)
        Me.StartPosition   = FormStartPosition.CenterParent
        Me.Font            = New Font("Segoe UI", 9)
        Me.BackColor       = Color.FromArgb(32, 35, 48)
        Me.ForeColor       = Color.FromArgb(200, 210, 230)

        ConstruireUI()
        ChargerValeursDepuisStyles()
    End Sub

    ' ─── Construction de l'interface ──────────────────────────────────────────

    Private Sub ConstruireUI()
        ' ── Section globale ──
        Dim pnlGlobal As New Panel() With {
            .Dock      = DockStyle.Top,
            .Height    = 162,
            .Padding   = New Padding(10, 8, 10, 6),
            .BackColor = Color.FromArgb(38, 42, 58)
        }

        Dim lblGlobal As New Label() With {
            .Text      = "APPARENCE GÉNÉRALE",
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(100, 150, 210),
            .AutoSize  = True,
            .Location  = New Point(10, 8)
        }

        ' Bouton fond
        Dim lblFond As New Label() With {
            .Text = "Fond :", .AutoSize = True, .Location = New Point(10, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _btnFond.Location  = New Point(55, 30)
        _btnFond.Size      = New Size(36, 26)
        _btnFond.FlatStyle = FlatStyle.Flat
        _btnFond.BackColor = ColorFromOxy(_stylesGraphique.CouleurFond)
        AddHandler _btnFond.Click, Sub(s, e) ChoisirCouleur(_btnFond, Sub(c) _stylesGraphique.CouleurFond = ToOxy(c))

        ' Bouton texte
        Dim lblTexte As New Label() With {
            .Text = "Texte :", .AutoSize = True, .Location = New Point(105, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _btnTexte.Location  = New Point(155, 30)
        _btnTexte.Size      = New Size(36, 26)
        _btnTexte.FlatStyle = FlatStyle.Flat
        _btnTexte.BackColor = ColorFromOxy(_stylesGraphique.CouleurTexte)
        AddHandler _btnTexte.Click, Sub(s, e) ChoisirCouleur(_btnTexte, Sub(c) _stylesGraphique.CouleurTexte = ToOxy(c))

        ' Bouton grille
        Dim lblGrille As New Label() With {
            .Text = "Grille :", .AutoSize = True, .Location = New Point(210, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _btnGrille.Location  = New Point(260, 30)
        _btnGrille.Size      = New Size(36, 26)
        _btnGrille.FlatStyle = FlatStyle.Flat
        _btnGrille.BackColor = ColorFromOxy(_stylesGraphique.CouleurGrille)
        AddHandler _btnGrille.Click, Sub(s, e) ChoisirCouleur(_btnGrille, Sub(c) _stylesGraphique.CouleurGrille = ToOxy(c))

        ' Style de grille
        Dim lblStyleGrille As New Label() With {
            .Text = "Style grille :", .AutoSize = True, .Location = New Point(315, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _cmbStyleGrille.Location      = New Point(410, 32)
        _cmbStyleGrille.Width         = 110
        _cmbStyleGrille.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbStyleGrille.Items.AddRange({"Pointillés", "Tirets", "Plein", "Aucune"})
        _cmbStyleGrille.SelectedIndex = 0
        AddHandler _cmbStyleGrille.SelectedIndexChanged, Sub(s, e)
            _stylesGraphique.StyleGrille = IndexVersLineStyle(_cmbStyleGrille.SelectedIndex)
        End Sub

        pnlGlobal.Controls.AddRange({lblGlobal, lblFond, _btnFond,
                                      lblTexte, _btnTexte,
                                      lblGrille, _btnGrille,
                                      lblStyleGrille, _cmbStyleGrille})

        ' ── Police axes ──
        Dim lblPolAxes As New Label() With {
            .Text = "Police axes (valeurs) :", .AutoSize = True,
            .Location = New Point(545, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _numPoliceAxes.Location     = New Point(680, 32)
        _numPoliceAxes.Width        = 55
        _numPoliceAxes.Minimum      = 5
        _numPoliceAxes.Maximum      = 18
        _numPoliceAxes.Value        = CDec(_stylesGraphique.TaillePoliceAxes)
        _numPoliceAxes.DecimalPlaces = 0
        AddHandler _numPoliceAxes.ValueChanged, Sub(s, e)
            _stylesGraphique.TaillePoliceAxes = CDbl(_numPoliceAxes.Value)
            AppliquerPreview()
        End Sub

        Dim lblPolAxesTitre As New Label() With {
            .Text = "Titres axes :", .AutoSize = True,
            .Location = New Point(750, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _numPoliceAxesTitre.Location     = New Point(830, 32)
        _numPoliceAxesTitre.Width        = 55
        _numPoliceAxesTitre.Minimum      = 5
        _numPoliceAxesTitre.Maximum      = 18
        _numPoliceAxesTitre.Value        = CDec(_stylesGraphique.TaillePoliceAxesTitre)
        _numPoliceAxesTitre.DecimalPlaces = 0
        AddHandler _numPoliceAxesTitre.ValueChanged, Sub(s, e)
            _stylesGraphique.TaillePoliceAxesTitre = CDbl(_numPoliceAxesTitre.Value)
            AppliquerPreview()
        End Sub

        Dim lblMargeBasse As New Label() With {
            .Text = "Marge axe X (px) :", .AutoSize = True,
            .Location = New Point(900, 36),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _numMargeBasse.Location      = New Point(1010, 32)
        _numMargeBasse.Width         = 60
        _numMargeBasse.Minimum       = 20
        _numMargeBasse.Maximum       = 150
        _numMargeBasse.Value         = CDec(_stylesGraphique.MargeBasse)
        _numMargeBasse.DecimalPlaces = 0
        _numMargeBasse.Increment     = 5
        Dim ttMarge As New ToolTip()
        ttMarge.SetToolTip(_numMargeBasse,
            "Espace en pixels entre l'axe X et le bord inférieur du graphique." & vbCrLf &
            "Augmenter si les valeurs de l'axe X sont coupées.")
        AddHandler _numMargeBasse.ValueChanged, Sub(s, e)
            _stylesGraphique.MargeBasse = CDbl(_numMargeBasse.Value)
            AppliquerPreview()
        End Sub

        pnlGlobal.Controls.AddRange({lblPolAxes, _numPoliceAxes, lblPolAxesTitre, _numPoliceAxesTitre,
                                      lblMargeBasse, _numMargeBasse})

        ' ── Ligne 2 : Légende ──
        Dim lblLegende As New Label() With {
            .Text      = "LÉGENDE",
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(100, 150, 210),
            .AutoSize  = True,
            .Location  = New Point(10, 72)
        }

        Dim lblFondLeg As New Label() With {
            .Text = "Fond :", .AutoSize = True, .Location = New Point(10, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _btnFondLegende.Location  = New Point(55, 94)
        _btnFondLegende.Size      = New Size(36, 26)
        _btnFondLegende.FlatStyle = FlatStyle.Flat
        _btnFondLegende.BackColor = ColorFromOxy(_stylesGraphique.CouleurFondLegende)
        AddHandler _btnFondLegende.Click, Sub(s, e) ChoisirCouleur(_btnFondLegende, Sub(c) _stylesGraphique.CouleurFondLegende = ToOxy(c))

        Dim lblTexteLeg As New Label() With {
            .Text = "Texte :", .AutoSize = True, .Location = New Point(105, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _btnTexteLegende.Location  = New Point(155, 94)
        _btnTexteLegende.Size      = New Size(36, 26)
        _btnTexteLegende.FlatStyle = FlatStyle.Flat
        _btnTexteLegende.BackColor = ColorFromOxy(_stylesGraphique.CouleurTexteLegende)
        AddHandler _btnTexteLegende.Click, Sub(s, e) ChoisirCouleur(_btnTexteLegende, Sub(c) _stylesGraphique.CouleurTexteLegende = ToOxy(c))

        Dim lblBordureLeg As New Label() With {
            .Text = "Bordure :", .AutoSize = True, .Location = New Point(210, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _btnBordureLegende.Location  = New Point(268, 94)
        _btnBordureLegende.Size      = New Size(36, 26)
        _btnBordureLegende.FlatStyle = FlatStyle.Flat
        _btnBordureLegende.BackColor = ColorFromOxy(_stylesGraphique.CouleurBordureLegende)
        AddHandler _btnBordureLegende.Click, Sub(s, e) ChoisirCouleur(_btnBordureLegende, Sub(c) _stylesGraphique.CouleurBordureLegende = ToOxy(c))

        Dim lblTailleLeg As New Label() With {
            .Text = "Police :", .AutoSize = True, .Location = New Point(320, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _numTaillePoliceLegende.Location  = New Point(370, 96)
        _numTaillePoliceLegende.Width     = 55
        _numTaillePoliceLegende.Minimum   = 6
        _numTaillePoliceLegende.Maximum   = 18
        _numTaillePoliceLegende.Value     = CDec(_stylesGraphique.TaillePoliceLegende)
        _numTaillePoliceLegende.DecimalPlaces = 0
        AddHandler _numTaillePoliceLegende.ValueChanged, Sub(s, e)
            _stylesGraphique.TaillePoliceLegende = CDbl(_numTaillePoliceLegende.Value)
            AppliquerPreview()
        End Sub

        Dim lblPosLeg As New Label() With {
            .Text = "Position :", .AutoSize = True, .Location = New Point(440, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _cmbPositionLegende.Location      = New Point(500, 96)
        _cmbPositionLegende.Width         = 130
        _cmbPositionLegende.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbPositionLegende.Items.AddRange({"Bas centre", "Bas gauche", "Bas droite",
                                            "Haut centre", "Haut gauche", "Haut droite",
                                            "Droite haut", "Droite bas", "Gauche haut"})
        _cmbPositionLegende.SelectedIndex = LegendPositionVersIndex(_stylesGraphique.PositionLegende)
        AddHandler _cmbPositionLegende.SelectedIndexChanged, Sub(s, e)
            _stylesGraphique.PositionLegende = IndexVersLegendPosition(_cmbPositionLegende.SelectedIndex)
            AppliquerPreview()
        End Sub

        Dim lblVisLeg As New Label() With {
            .Text = "Visible :", .AutoSize = True, .Location = New Point(648, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _chkLegendeVisible.Location = New Point(700, 98)
        _chkLegendeVisible.Checked  = _stylesGraphique.LegendeVisible
        _chkLegendeVisible.AutoSize = True
        AddHandler _chkLegendeVisible.CheckedChanged, Sub(s, e)
            _stylesGraphique.LegendeVisible = _chkLegendeVisible.Checked
            AppliquerPreview()
        End Sub

        Dim lblPlacLeg As New Label() With {
            .Text = "Placement :", .AutoSize = True, .Location = New Point(750, 100),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _cmbPlacementLegende.Location      = New Point(820, 96)
        _cmbPlacementLegende.Width         = 80
        _cmbPlacementLegende.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbPlacementLegende.Items.AddRange({"Intérieur", "Extérieur"})
        _cmbPlacementLegende.SelectedIndex = If(_stylesGraphique.PlacementLegende =
            OxyPlot.Legends.LegendPlacement.Outside, 1, 0)
        AddHandler _cmbPlacementLegende.SelectedIndexChanged, Sub(s, e)
            _stylesGraphique.PlacementLegende = If(_cmbPlacementLegende.SelectedIndex = 1,
                OxyPlot.Legends.LegendPlacement.Outside,
                OxyPlot.Legends.LegendPlacement.Inside)
            AppliquerPreview()
        End Sub

        Dim lblMargeLeg As New Label() With {
            .Text = "Marge ext. :", .AutoSize = True, .Location = New Point(10, 126),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _numMargeLegende.Location     = New Point(90, 122)
        _numMargeLegende.Width        = 55
        _numMargeLegende.Minimum      = 0
        _numMargeLegende.Maximum      = 40
        _numMargeLegende.Value        = CDec(_stylesGraphique.MargeLegende)
        _numMargeLegende.DecimalPlaces = 0
        AddHandler _numMargeLegende.ValueChanged, Sub(s, e)
            _stylesGraphique.MargeLegende = CDbl(_numMargeLegende.Value)
            AppliquerPreview()
        End Sub

        Dim lblPaddingLeg As New Label() With {
            .Text = "Marge int. :", .AutoSize = True, .Location = New Point(160, 126),
            .ForeColor = Color.FromArgb(180, 190, 210)
        }
        _numPaddingLegende.Location      = New Point(240, 122)
        _numPaddingLegende.Width         = 55
        _numPaddingLegende.Minimum       = 0
        _numPaddingLegende.Maximum       = 20
        _numPaddingLegende.Value         = CDec(_stylesGraphique.PaddingLegende)
        _numPaddingLegende.DecimalPlaces = 0
        AddHandler _numPaddingLegende.ValueChanged, Sub(s, e)
            _stylesGraphique.PaddingLegende = CDbl(_numPaddingLegende.Value)
            AppliquerPreview()
        End Sub

        pnlGlobal.Controls.AddRange({
            lblLegende,
            lblFondLeg,    _btnFondLegende,
            lblTexteLeg,   _btnTexteLegende,
            lblBordureLeg, _btnBordureLegende,
            lblTailleLeg,  _numTaillePoliceLegende,
            lblPosLeg,     _cmbPositionLegende,
            lblVisLeg,     _chkLegendeVisible,
            lblPlacLeg,    _cmbPlacementLegende,
            lblMargeLeg,   _numMargeLegende,
            lblPaddingLeg, _numPaddingLegende
        })

        ' ── Grille des séries ──
        _dgv.Dock                  = DockStyle.Fill
        _dgv.AllowUserToAddRows    = False
        _dgv.AllowUserToDeleteRows = False
        _dgv.RowHeadersVisible     = False
        _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgv.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgv.Font                  = New Font("Consolas", 8.5)
        _dgv.BackgroundColor       = Color.FromArgb(22, 25, 36)
        _dgv.GridColor             = Color.FromArgb(45, 50, 68)
        _dgv.BorderStyle           = BorderStyle.None
        _dgv.DefaultCellStyle.BackColor          = Color.FromArgb(26, 29, 42)
        _dgv.DefaultCellStyle.ForeColor          = Color.FromArgb(200, 210, 230)
        _dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 80, 130)
        _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 40, 58)
        _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(140, 160, 200)
        _dgv.ColumnHeadersDefaultCellStyle.Font      = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgv.EnableHeadersVisualStyles = False

        ' Colonnes
        Dim colNom As New DataGridViewTextBoxColumn() With {
            .Name = "cNom", .HeaderText = "Voie", .ReadOnly = True, .FillWeight = 30
        }
        Dim colCentrale As New DataGridViewTextBoxColumn() With {
            .Name = "cCentrale", .HeaderText = "Centrale", .ReadOnly = True, .FillWeight = 20
        }
        Dim colCouleur As New DataGridViewButtonColumn() With {
            .Name = "cCouleur", .HeaderText = "Couleur", .FillWeight = 12
        }
        Dim colStyle As New DataGridViewComboBoxColumn() With {
            .Name = "cStyle", .HeaderText = "Style ligne", .FillWeight = 18
        }
        colStyle.Items.AddRange({"Plein", "Tirets", "Pointillés", "Tirets-Points", "Aucun"})

        Dim colMarqueur As New DataGridViewComboBoxColumn() With {
            .Name = "cMarqueur", .HeaderText = "Marqueur", .FillWeight = 18
        }
        colMarqueur.Items.AddRange({"Aucun", "Cercle", "Carré", "Triangle", "Croix", "Plus", "Étoile", "Diamant"})

        Dim colEpaisseur As New DataGridViewTextBoxColumn() With {
            .Name = "cEpaisseur", .HeaderText = "Épaisseur", .FillWeight = 12
        }
        Dim colTailleMarq As New DataGridViewTextBoxColumn() With {
            .Name = "cTailleMarq", .HeaderText = "T. marq.", .FillWeight = 10
        }

        _dgv.Columns.AddRange({colNom, colCentrale, colCouleur, colStyle, colMarqueur, colEpaisseur, colTailleMarq})

        AddHandler _dgv.CellClick,       AddressOf Dgv_CellClick
        AddHandler _dgv.CellValueChanged, AddressOf Dgv_CellValueChanged
        AddHandler _dgv.CurrentCellDirtyStateChanged, Sub(s, e)
            If _dgv.IsCurrentCellDirty Then _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End Sub

        ' ── Boutons bas ──
        Dim pnlBas As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Bottom,
            .Height        = 44,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding       = New Padding(8, 6, 8, 0),
            .BackColor     = Color.FromArgb(38, 42, 58)
        }

        _btnOK.Text      = "✔  Appliquer et fermer"
        _btnOK.BackColor = Color.FromArgb(40, 110, 60)
        _btnOK.ForeColor = Color.White
        _btnOK.FlatStyle = FlatStyle.Flat
        _btnOK.AutoSize  = True
        _btnOK.Height    = 30

        _btnAnnuler.Text      = "✕  Annuler"
        _btnAnnuler.BackColor = Color.FromArgb(90, 40, 40)
        _btnAnnuler.ForeColor = Color.White
        _btnAnnuler.FlatStyle = FlatStyle.Flat
        _btnAnnuler.AutoSize  = True
        _btnAnnuler.Height    = 30
        _btnAnnuler.Margin    = New Padding(6, 0, 0, 0)

        _btnRAZ.Text      = "⟳  RAZ"
        _btnRAZ.BackColor = Color.FromArgb(80, 60, 20)
        _btnRAZ.ForeColor = Color.White
        _btnRAZ.FlatStyle = FlatStyle.Flat
        _btnRAZ.AutoSize  = True
        _btnRAZ.Height    = 30
        _btnRAZ.Margin    = New Padding(6, 0, 0, 0)

        Dim tt As New ToolTip()
        tt.SetToolTip(_btnRAZ, "Réinitialise toutes les personnalisations aux valeurs par défaut")

        AddHandler _btnOK.Click,      AddressOf BtnOK_Click
        AddHandler _btnAnnuler.Click, AddressOf BtnAnnuler_Click
        AddHandler _btnRAZ.Click,     AddressOf BtnRAZ_Click

        pnlBas.Controls.AddRange({_btnOK, _btnAnnuler, _btnRAZ})

        ' ── Titre grille ──
        Dim lblSeries As New Label() With {
            .Text      = "PERSONNALISATION PAR VOIE",
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(100, 150, 210),
            .Dock      = DockStyle.Top,
            .Height    = 24,
            .Padding   = New Padding(10, 6, 0, 0),
            .BackColor = Color.FromArgb(38, 42, 58)
        }

        Dim pnlGrille As New Panel() With {.Dock = DockStyle.Fill}
        pnlGrille.Controls.Add(_dgv)
        pnlGrille.Controls.Add(lblSeries)

        Me.Controls.Add(pnlGrille)
        Me.Controls.Add(pnlBas)
        Me.Controls.Add(pnlGlobal)
    End Sub

    ' ─── Chargement des valeurs depuis les styles ──────────────────────────────

    Private Sub ChargerValeursDepuisStyles()
        ' Contrôles globaux
        _btnFond.BackColor   = ColorFromOxy(_stylesGraphique.CouleurFond)
        _btnTexte.BackColor  = ColorFromOxy(_stylesGraphique.CouleurTexte)
        _btnGrille.BackColor = ColorFromOxy(_stylesGraphique.CouleurGrille)
        _cmbStyleGrille.SelectedIndex = LineStyleVersIndex(_stylesGraphique.StyleGrille)
        _numPoliceAxes.Value      = CDec(_stylesGraphique.TaillePoliceAxes)
        _numPoliceAxesTitre.Value = CDec(_stylesGraphique.TaillePoliceAxesTitre)
        _numMargeBasse.Value      = CDec(_stylesGraphique.MargeBasse)
        ' Légende
        _btnFondLegende.BackColor         = ColorFromOxy(_stylesGraphique.CouleurFondLegende)
        _btnTexteLegende.BackColor        = ColorFromOxy(_stylesGraphique.CouleurTexteLegende)
        _btnBordureLegende.BackColor      = ColorFromOxy(_stylesGraphique.CouleurBordureLegende)
        _numTaillePoliceLegende.Value     = CDec(_stylesGraphique.TaillePoliceLegende)
        _cmbPositionLegende.SelectedIndex = LegendPositionVersIndex(_stylesGraphique.PositionLegende)
        _chkLegendeVisible.Checked        = _stylesGraphique.LegendeVisible
        _numMargeLegende.Value            = CDec(_stylesGraphique.MargeLegende)
        _numPaddingLegende.Value          = CDec(_stylesGraphique.PaddingLegende)

        ' Grille des séries
        _dgv.Rows.Clear()
        For Each sg In _series
            Dim style = _stylesGraphique.ObtenirStyle(sg.Cle, sg.Couleur)
            Dim idx   = _dgv.Rows.Add(
                sg.Nom,
                sg.NomCentrale,
                "■",                                          ' bouton couleur
                LineStyleVersLibelle(style.StyleLigne),
                MarqueurVersLibelle(style.Marqueur),
                style.Epaisseur.ToString("F1"),
                style.TailleMarqueur.ToString("F0"))

            ' Colorier le bouton avec la couleur de la série
            _dgv.Rows(idx).Tag = sg.Cle
            _dgv.Rows(idx).Cells("cCouleur").Style.BackColor = ColorFromOxy(style.Couleur)
            _dgv.Rows(idx).Cells("cCouleur").Style.ForeColor = ColorFromOxy(style.Couleur)
        Next
    End Sub

    ' ─── Événements grille ────────────────────────────────────────────────────

    Private Sub Dgv_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
        If _dgv.Columns(e.ColumnIndex).Name <> "cCouleur" Then Return

        Dim cle = _dgv.Rows(e.RowIndex).Tag?.ToString()
        If String.IsNullOrEmpty(cle) Then Return

        Dim sg    = _series.FirstOrDefault(Function(x) x.Cle = cle)
        If sg Is Nothing Then Return
        Dim style = _stylesGraphique.ObtenirStyle(cle, sg.Couleur)

        Using dlg As New ColorDialog() With {.Color = ColorFromOxy(style.Couleur), .FullOpen = True}
            If dlg.ShowDialog() = DialogResult.OK Then
                style.Couleur = ToOxy(dlg.Color)
                _stylesGraphique.DefinirStyle(cle, style)
                _dgv.Rows(e.RowIndex).Cells("cCouleur").Style.BackColor = dlg.Color
                _dgv.Rows(e.RowIndex).Cells("cCouleur").Style.ForeColor = dlg.Color
                AppliquerPreview()
            End If
        End Using
    End Sub

    Private Sub Dgv_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        Dim cle = _dgv.Rows(e.RowIndex).Tag?.ToString()
        If String.IsNullOrEmpty(cle) Then Return
        Dim sg = _series.FirstOrDefault(Function(x) x.Cle = cle)
        If sg Is Nothing Then Return

        Dim style = _stylesGraphique.ObtenirStyle(cle, sg.Couleur)

        Select Case _dgv.Columns(e.ColumnIndex).Name
            Case "cStyle"
                Dim v = TryCast(_dgv.Rows(e.RowIndex).Cells("cStyle").Value, String)
                If v IsNot Nothing Then style.StyleLigne = LibelleVersLineStyle(v)
            Case "cMarqueur"
                Dim v = TryCast(_dgv.Rows(e.RowIndex).Cells("cMarqueur").Value, String)
                If v IsNot Nothing Then style.Marqueur = LibelleVersMarqueur(v)
            Case "cEpaisseur"
                Dim v As Double
                If Double.TryParse(_dgv.Rows(e.RowIndex).Cells("cEpaisseur").Value?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.CurrentCulture, v) Then
                    style.Epaisseur = Math.Max(0.5, Math.Min(8.0, v))
                End If
            Case "cTailleMarq"
                Dim v As Double
                If Double.TryParse(_dgv.Rows(e.RowIndex).Cells("cTailleMarq").Value?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.CurrentCulture, v) Then
                    style.TailleMarqueur = Math.Max(2.0, Math.Min(20.0, v))
                End If
        End Select

        _stylesGraphique.DefinirStyle(cle, style)
        AppliquerPreview()
    End Sub

    ' ─── Boutons ──────────────────────────────────────────────────────────────

    Private Sub BtnOK_Click(sender As Object, e As EventArgs)
        _onAppliquer?.Invoke()
        Me.Close()
    End Sub

    Private Sub BtnAnnuler_Click(sender As Object, e As EventArgs)
        ' Restaurer l'état avant ouverture
        _stylesGraphique.RestaurerDepuis(_sauvegardeAvant)
        _onAppliquer?.Invoke()
        Me.Close()
    End Sub

    Private Sub BtnRAZ_Click(sender As Object, e As EventArgs)
        Dim rep = MessageBox.Show(
            "Réinitialiser toutes les personnalisations aux valeurs par défaut ?",
            "RAZ personnalisation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If rep <> DialogResult.Yes Then Return

        _stylesGraphique.ResetTout(_series)
        ChargerValeursDepuisStyles()
        AppliquerPreview()
    End Sub

    Private Sub AppliquerPreview()
        _onAppliquer?.Invoke()
    End Sub

    Private Sub ChoisirCouleur(btn As Button, appliquer As Action(Of Color))
        Using dlg As New ColorDialog() With {.Color = btn.BackColor, .FullOpen = True}
            If dlg.ShowDialog() = DialogResult.OK Then
                btn.BackColor = dlg.Color
                appliquer(dlg.Color)
                AppliquerPreview()
            End If
        End Using
    End Sub

    ' ─── Conversions style ────────────────────────────────────────────────────

    Private Shared Function LineStyleVersIndex(ls As LineStyle) As Integer
        Select Case ls
            Case LineStyle.Dot       : Return 0
            Case LineStyle.Dash      : Return 1
            Case LineStyle.Solid     : Return 2
            Case LineStyle.DashDot   : Return 3
            Case Else                : Return 0
        End Select
    End Function

    Private Shared Function IndexVersLineStyle(idx As Integer) As LineStyle
        Select Case idx
            Case 0 : Return LineStyle.Dot
            Case 1 : Return LineStyle.Dash
            Case 2 : Return LineStyle.Solid
            Case 3 : Return LineStyle.DashDot
            Case 4 : Return LineStyle.None
            Case Else : Return LineStyle.Dot
        End Select
    End Function

    Private Shared Function LineStyleVersLibelle(ls As LineStyle) As String
        Select Case ls
            Case LineStyle.Solid   : Return "Plein"
            Case LineStyle.Dash    : Return "Tirets"
            Case LineStyle.Dot     : Return "Pointillés"
            Case LineStyle.DashDot : Return "Tirets-Points"
            Case LineStyle.None    : Return "Aucun"
            Case Else              : Return "Plein"
        End Select
    End Function

    Private Shared Function LibelleVersLineStyle(s As String) As LineStyle
        Select Case s
            Case "Plein"         : Return LineStyle.Solid
            Case "Tirets"        : Return LineStyle.Dash
            Case "Pointillés"    : Return LineStyle.Dot
            Case "Tirets-Points" : Return LineStyle.DashDot
            Case "Aucun"         : Return LineStyle.None
            Case Else            : Return LineStyle.Solid
        End Select
    End Function

    Private Shared Function MarqueurVersLibelle(m As MarkerType) As String
        Select Case m
            Case MarkerType.None     : Return "Aucun"
            Case MarkerType.Circle   : Return "Cercle"
            Case MarkerType.Square   : Return "Carré"
            Case MarkerType.Triangle : Return "Triangle"
            Case MarkerType.Cross    : Return "Croix"
            Case MarkerType.Plus     : Return "Plus"
            Case MarkerType.Star     : Return "Étoile"
            Case MarkerType.Diamond  : Return "Diamant"
            Case Else                : Return "Aucun"
        End Select
    End Function

    Private Shared Function LibelleVersMarqueur(s As String) As MarkerType
        Select Case s
            Case "Aucun"    : Return MarkerType.None
            Case "Cercle"   : Return MarkerType.Circle
            Case "Carré"    : Return MarkerType.Square
            Case "Triangle" : Return MarkerType.Triangle
            Case "Croix"    : Return MarkerType.Cross
            Case "Plus"     : Return MarkerType.Plus
            Case "Étoile"   : Return MarkerType.Star
            Case "Diamant"  : Return MarkerType.Diamond
            Case Else       : Return MarkerType.None
        End Select
    End Function

    Private Shared Function ToOxy(c As Color) As OxyColor
        Return OxyColor.FromArgb(c.A, c.R, c.G, c.B)
    End Function

    Private Shared Function ColorFromOxy(c As OxyColor) As Color
        Return Color.FromArgb(c.A, c.R, c.G, c.B)
    End Function

    Private Shared Function LegendPositionVersIndex(pos As OxyPlot.Legends.LegendPosition) As Integer
        Select Case pos
            Case OxyPlot.Legends.LegendPosition.BottomCenter : Return 0
            Case OxyPlot.Legends.LegendPosition.BottomLeft   : Return 1
            Case OxyPlot.Legends.LegendPosition.BottomRight  : Return 2
            Case OxyPlot.Legends.LegendPosition.TopCenter    : Return 3
            Case OxyPlot.Legends.LegendPosition.TopLeft      : Return 4
            Case OxyPlot.Legends.LegendPosition.TopRight     : Return 5
            Case OxyPlot.Legends.LegendPosition.RightTop     : Return 6
            Case OxyPlot.Legends.LegendPosition.RightBottom  : Return 7
            Case OxyPlot.Legends.LegendPosition.LeftTop      : Return 8
            Case Else : Return 0
        End Select
    End Function

    Private Shared Function IndexVersLegendPosition(idx As Integer) As OxyPlot.Legends.LegendPosition
        Select Case idx
            Case 0 : Return OxyPlot.Legends.LegendPosition.BottomCenter
            Case 1 : Return OxyPlot.Legends.LegendPosition.BottomLeft
            Case 2 : Return OxyPlot.Legends.LegendPosition.BottomRight
            Case 3 : Return OxyPlot.Legends.LegendPosition.TopCenter
            Case 4 : Return OxyPlot.Legends.LegendPosition.TopLeft
            Case 5 : Return OxyPlot.Legends.LegendPosition.TopRight
            Case 6 : Return OxyPlot.Legends.LegendPosition.RightTop
            Case 7 : Return OxyPlot.Legends.LegendPosition.RightBottom
            Case 8 : Return OxyPlot.Legends.LegendPosition.LeftTop
            Case Else : Return OxyPlot.Legends.LegendPosition.BottomCenter
        End Select
    End Function

End Class
