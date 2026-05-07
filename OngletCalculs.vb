Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>
''' Onglet "Calculs" — permet de définir des voies calculées à partir
''' des voies mesurées de toutes les centrales.
'''
''' Interface :
'''   - Tableau des calculs définis (Nom, Unité, Expression, Nb points moy., Actif)
'''   - Zone d'édition avec liste déroulante d'insertion des voies
'''   - Bouton "Tester" pour vérifier la formule avec les dernières valeurs
'''   - Bouton "Sauvegarder"
''' </summary>
Public Class OngletCalculs

    ' ─── Propriétés ───────────────────────────────────────────────────────────

    Public Property Config        As ConfigManager
    Public Property Gestionnaire  As GestionnaireMultiCentrale
    Public Property GestCalculs   As GestionnaireCalculs
    Public Property Historique    As HistoriqueMultiCentrale

    ' ─── Événements ───────────────────────────────────────────────────────────

    Public Event CalculsModifies(sender As Object)
    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Contrôles ────────────────────────────────────────────────────────────

    Private _dgv            As New DataGridView()
    Private _btnAjouter     As New Button()
    Private _btnSupprimer   As New Button()
    Private _btnMonter      As New Button()
    Private _btnDescendre   As New Button()
    Private _btnSauver      As New Button()
    Private _btnTester      As New Button()

    Private _pnlEditeur     As New Panel()
    Private _txtNom         As New TextBox()
    Private _txtUnite       As New TextBox()
    Private _txtExpression  As New RichTextBox()
    Private _numNbMoy       As New NumericUpDown()
    Private _cmbVoies       As New ComboBox()
    Private _btnInserer      As New Button()
    Private _lblResultat    As New Label()
    Private _lblAide        As New Label()

    Private _indexEdite       As Integer = -1   ' index de la ligne en cours d'édition
    Private _chargementEdit   As Boolean = False ' bloque la synchro pendant chargement

    ' ─── Construction ─────────────────────────────────────────────────────────

    Public Function ConstruirePanel() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' ── Barre d'outils ──
        Dim tb As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top, .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding = New Padding(6, 6, 6, 4),
            .BackColor = Color.FromArgb(35, 38, 52)
        }

        StylerBouton(_btnAjouter,   "＋ Ajouter",    Color.FromArgb(40, 110, 60))
        StylerBouton(_btnSupprimer, "✕ Supprimer",   Color.FromArgb(110, 40, 40))
        StylerBouton(_btnMonter,    "▲",              Color.FromArgb(60, 65, 85))
        StylerBouton(_btnDescendre, "▼",              Color.FromArgb(60, 65, 85))
        StylerBouton(_btnSauver,    "💾 Sauvegarder", Color.FromArgb(55, 65, 90))
        _btnMonter.Width    = 32
        _btnDescendre.Width = 32

        Dim tt As New ToolTip()
        tt.SetToolTip(_btnAjouter,   "Créer un nouveau calcul")
        tt.SetToolTip(_btnSupprimer, "Supprimer le calcul sélectionné")
        tt.SetToolTip(_btnMonter,    "Monter dans la liste")
        tt.SetToolTip(_btnDescendre, "Descendre dans la liste")
        tt.SetToolTip(_btnSauver,    "Sauvegarder tous les calculs dans config.ini")

        tb.Controls.AddRange({_btnAjouter, _btnSupprimer,
                               _btnMonter, _btnDescendre,
                               _btnSauver})

        ' ── Tableau des calculs (hauteur fixe, redimensionnable) ──
        ConstruireGrille()
        Dim pnlTableau As New Panel() With {
            .Dock = DockStyle.Top, .Height = 160,
            .BackColor = Color.White}
        pnlTableau.Controls.Add(_dgv)

        ' Poignée de redimensionnement entre tableau et éditeur
        Dim splitter As New Splitter() With {
            .Dock = DockStyle.Top, .Height = 4,
            .BackColor = Color.FromArgb(180, 200, 230)}

        ' ── Éditeur d'expression (remplit l'espace restant, scrollable) ──
        pnl.Controls.Add(ConstruireEditeur())  ' Fill — ajouté en premier
        pnl.Controls.Add(splitter)             ' Top
        pnl.Controls.Add(pnlTableau)           ' Top
        pnl.Controls.Add(tb)                   ' Top — barre d'outils

        ' Événements
        AddHandler _btnAjouter.Click,   AddressOf BtnAjouter_Click
        AddHandler _btnSupprimer.Click, AddressOf BtnSupprimer_Click
        AddHandler _btnMonter.Click,    AddressOf BtnMonter_Click
        AddHandler _btnDescendre.Click, AddressOf BtnDescendre_Click
        AddHandler _btnSauver.Click,    AddressOf BtnSauver_Click
        AddHandler _btnTester.Click,    AddressOf BtnTester_Click
        AddHandler _btnInserer.Click,   AddressOf BtnInserer_Click
        AddHandler _dgv.SelectionChanged, AddressOf Dgv_SelectionChanged
        AddHandler _dgv.CellValueChanged, AddressOf Dgv_CellValueChanged
        AddHandler _dgv.CurrentCellDirtyStateChanged, Sub(s, e)
            If _dgv.IsCurrentCellDirty Then _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End Sub

        ' Synchronisation en temps réel éditeur → tableau
        AddHandler _txtNom.TextChanged,        Sub(s, e) AppliquerEditeurVersCalcul()
        AddHandler _txtUnite.TextChanged,      Sub(s, e) AppliquerEditeurVersCalcul()
        AddHandler _txtExpression.TextChanged, Sub(s, e) AppliquerEditeurVersCalcul()
        AddHandler _numNbMoy.ValueChanged,     Sub(s, e) AppliquerEditeurVersCalcul()

        Return pnl
    End Function

    Private Sub ConstruireGrille()
        _dgv.Dock                  = DockStyle.Fill
        _dgv.AllowUserToAddRows    = False
        _dgv.AllowUserToDeleteRows = False
        _dgv.RowHeadersVisible     = False
        _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgv.MultiSelect           = False
        _dgv.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgv.Font                  = New Font("Segoe UI", 9)
        _dgv.BackgroundColor       = Color.White
        _dgv.GridColor             = Color.FromArgb(200, 210, 230)
        _dgv.BorderStyle           = BorderStyle.None
        _dgv.DefaultCellStyle.BackColor          = Color.White
        _dgv.DefaultCellStyle.ForeColor          = Color.FromArgb(30, 40, 70)
        _dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(180, 210, 245)
        _dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 80)
        _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(210, 225, 250)
        _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 55, 110)
        _dgv.ColumnHeadersDefaultCellStyle.Font      = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgv.EnableHeadersVisualStyles = False

        _dgv.Columns.AddRange({
            New DataGridViewCheckBoxColumn() With {
                .Name = "cActif", .HeaderText = "Actif", .Width = 50, .FillWeight = 8
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cNom", .HeaderText = "Nom [Calcul]", .ReadOnly = True, .FillWeight = 22
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cUnite", .HeaderText = "Unité", .ReadOnly = True, .FillWeight = 10
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cExpression", .HeaderText = "Expression", .ReadOnly = True, .FillWeight = 50
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cNbMoy", .HeaderText = "Moy.", .ReadOnly = True, .FillWeight = 10,
                .ToolTipText = "Nombre de points pour la moyenne glissante (1 = instantané)"
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cValeur", .HeaderText = "Dernière valeur", .ReadOnly = True, .FillWeight = 15
            }
        })
    End Sub

    Private Function ConstruireEditeur() As Control
        _pnlEditeur.Dock      = DockStyle.Fill
        _pnlEditeur.BackColor = Color.White
        _pnlEditeur.AutoScroll = True

        Dim tbl As New TableLayoutPanel() With {
            .Dock         = DockStyle.Top,
            .AutoSize     = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount  = 1,
            .RowCount     = 7,
            .BackColor    = Color.White,
            .Padding      = New Padding(6, 6, 6, 40)
        }
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 36))  ' Nom / Unité / Moy / Tester
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))  ' Insérer voie
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))  ' label Expression
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 90))  ' zone expression
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 24))  ' résultat test
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))  ' bouton toggle aide
        tbl.RowStyles.Add(New RowStyle(SizeType.AutoSize))       ' aide syntaxe — hauteur automatique

        ' ── Ligne 0 : Nom, Unité, Moy., Tester ──
        Dim pnlL1 As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill, .AutoSize = False, .BackColor = Color.White
        }
        Dim lblNom As New Label() With {
            .Text = "Nom :", .AutoSize = True, .Margin = New Padding(0, 8, 4, 0),
            .ForeColor = Color.FromArgb(40, 60, 100)
        }
        _txtNom.Width     = 160
        _txtNom.Font      = New Font("Segoe UI", 9)
        _txtNom.BackColor = Color.FromArgb(245, 248, 255)
        _txtNom.ForeColor = Color.Black
        _txtNom.Margin    = New Padding(0, 4, 8, 0)

        Dim lblUnite As New Label() With {
            .Text = "Unité :", .AutoSize = True, .Margin = New Padding(0, 8, 4, 0),
            .ForeColor = Color.FromArgb(40, 60, 100)
        }
        _txtUnite.Width     = 70
        _txtUnite.Font      = New Font("Segoe UI", 9)
        _txtUnite.BackColor = Color.FromArgb(245, 248, 255)
        _txtUnite.ForeColor = Color.Black
        _txtUnite.Margin    = New Padding(0, 4, 8, 0)

        Dim lblMoy As New Label() With {
            .Text = "Moy. glissante (pts) :", .AutoSize = True, .Margin = New Padding(0, 8, 4, 0),
            .ForeColor = Color.FromArgb(40, 60, 100)
        }
        _numNbMoy.Minimum = 1 : _numNbMoy.Maximum = 100 : _numNbMoy.Value = 1
        _numNbMoy.Width   = 55
        _numNbMoy.Margin  = New Padding(0, 4, 8, 0)

        StylerBouton(_btnTester, "▶ Tester", Color.FromArgb(40, 100, 160))
        _btnTester.Margin = New Padding(12, 2, 0, 0)

        pnlL1.Controls.AddRange({lblNom, _txtNom, lblUnite, _txtUnite,
                                  lblMoy, _numNbMoy, _btnTester})

        ' ── Ligne 1 : Insérer voie ──
        Dim pnlL2 As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill, .AutoSize = False, .BackColor = Color.White
        }
        Dim lblInsert As New Label() With {
            .Text = "Insérer une voie :", .AutoSize = True, .Margin = New Padding(0, 6, 4, 0),
            .ForeColor = Color.FromArgb(40, 60, 100)
        }
        _cmbVoies.Width         = 340
        _cmbVoies.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbVoies.Font          = New Font("Consolas", 8.5)
        _cmbVoies.BackColor     = Color.FromArgb(245, 248, 255)
        _cmbVoies.ForeColor     = Color.Black
        _cmbVoies.Margin        = New Padding(0, 2, 6, 0)

        StylerBouton(_btnInserer, "← Insérer", Color.FromArgb(70, 80, 110))
        _btnInserer.Margin = New Padding(0, 2, 0, 0)

        pnlL2.Controls.AddRange({lblInsert, _cmbVoies, _btnInserer})

        ' ── Ligne 2 : label Expression ──
        Dim lblExpr As New Label() With {
            .Text      = "Expression mathématique :",
            .Dock      = DockStyle.Fill,
            .Font      = New Font("Segoe UI", 8.5, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 60, 100),
            .Margin    = New Padding(0)
        }

        ' ── Ligne 3 : zone expression ──
        _txtExpression.Dock        = DockStyle.Fill
        _txtExpression.Font        = New Font("Consolas", 10)
        _txtExpression.BackColor   = Color.FromArgb(245, 248, 255)
        _txtExpression.ForeColor   = Color.FromArgb(20, 60, 140)
        _txtExpression.BorderStyle = BorderStyle.FixedSingle
        _txtExpression.ScrollBars  = RichTextBoxScrollBars.Vertical
        _txtExpression.Margin      = New Padding(0)
        _txtExpression.Height      = 88

        ' ── Ligne 4 : résultat test ──
        _lblResultat.Dock      = DockStyle.Fill
        _lblResultat.Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        _lblResultat.ForeColor = Color.FromArgb(0, 140, 70)
        _lblResultat.Text      = ""
        _lblResultat.Margin    = New Padding(0)

        Dim btnAide As New Button() With {
            .Text = "▼ Aide syntaxe", .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 7.5, FontStyle.Bold),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(200, 215, 240),
            .ForeColor = Color.FromArgb(20, 40, 80),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)}
        btnAide.FlatAppearance.BorderSize = 0

        ' ── Ligne 6 : aide syntaxe — hauteur automatique depuis le contenu ──
        Dim pnlAide As New Panel() With {
            .Dock         = DockStyle.Top,
            .AutoSize     = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .BackColor    = Color.FromArgb(230, 240, 255),
            .Padding      = New Padding(8, 4, 8, 8),
            .Margin       = New Padding(0),
            .Visible      = False}
        _lblAide.Dock      = DockStyle.Top
        _lblAide.AutoSize  = True
        _lblAide.Font = New Font("Segoe UI", 8)
        _lblAide.ForeColor = Color.FromArgb(20, 40, 80)
        _lblAide.BackColor = Color.Transparent
        _lblAide.Text = "Opérateurs : + − * / ^ (puissance)   Fonctions : abs sqrt ln log10 exp sin cos tan min(a,b) max(a,b) floor ceil round" & vbCrLf &
                        "Constantes : pi  e" & vbCrLf &
                        "Référence voie : {C1_V101}   Référence calcul : {CALC_Id}" & vbCrLf &
                        "Intégration (trapèzes, interpolation aux bornes) :" & vbCrLf &
                        "  int(expr, t_debut, t_fin) — bornes optionnelles [s][min][h][j]" & vbCrLf &
                        "  NE PAS écrire *dt dans expr — int() applique déjà le pas d'acquisition dt" & vbCrLf &
                        "  int({C1_V101},,)        intégrale depuis le début" & vbCrLf &
                        "  int({C1_V101},30[min],2[h])  entre 30min et 2h" & vbCrLf &
                        "  Exemple énergie Wh : int({C1_V101}*4180*({C1_V102}-{C1_V103})/3600,,)"
        _lblAide.Margin = New Padding(0)
        pnlAide.Controls.Add(_lblAide)

        ' Toggle aide — Visible suffit : AutoSize recalcule la hauteur du tbl
        AddHandler btnAide.Click, Sub(s, e)
            pnlAide.Visible = Not pnlAide.Visible
            btnAide.Text = If(pnlAide.Visible, "▲ Aide syntaxe", "▼ Aide syntaxe")
        End Sub

        tbl.Controls.Add(pnlL1,          0, 0)
        tbl.Controls.Add(pnlL2,          0, 1)
        tbl.Controls.Add(lblExpr,        0, 2)
        tbl.Controls.Add(_txtExpression, 0, 3)
        tbl.Controls.Add(_lblResultat,   0, 4)
        tbl.Controls.Add(btnAide,        0, 5)
        tbl.Controls.Add(pnlAide,        0, 6)

        _pnlEditeur.Controls.Add(tbl)
        Return _pnlEditeur
    End Function

    ' ─── Chargement depuis GestCalculs ────────────────────────────────────────

    Public Sub RemplirGrille()
        _dgv.Rows.Clear()
        For Each vc In GestCalculs.Voies
            Dim idx = _dgv.Rows.Add(
                vc.Active,
                vc.Nom,
                vc.Unite,
                vc.Expression,
                vc.NbPointsMoyenne.ToString(),
                If(vc.EnErreur, "ERR", If(Double.IsNaN(vc.Valeur), "---",
                    vc.Valeur.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) & " " & vc.Unite)))
            _dgv.Rows(idx).Tag = vc.Id
        Next
        ActualiserListeVoies()
    End Sub

    ''' <summary>Actualise la liste déroulante des voies disponibles.</summary>
    Public Sub ActualiserListeVoies()
        _cmbVoies.Items.Clear()
        If Gestionnaire Is Nothing Then Return
        For Each c In Gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                Dim cle = HistoriqueMultiCentrale.CleVoie(c.Numero, v.Numero)
                _cmbVoies.Items.Add(New VoieItem(cle,
                    String.Format("[{0}] {1} ({2})  →  {{{3}}}",
                        c.NomAffiche, v.Nom, v.Unite, cle)))
            Next
            For Each s In c.Voies.SortiesActives()
                Dim cle = HistoriqueMultiCentrale.CleSortie(c.Numero, s.Numero)
                _cmbVoies.Items.Add(New VoieItem(cle,
                    String.Format("[{0}] {1} (ON/OFF)  →  {{{2}}}",
                        c.NomAffiche, s.Nom, cle)))
            Next
        Next
        ' Ajouter les voies calculées existantes
        For Each vc In GestCalculs.Voies
            _cmbVoies.Items.Add(New VoieItem(vc.CleHistorique,
                String.Format("[Calcul] {0} ({1})  →  {{{2}}}",
                    vc.Nom, vc.Unite, vc.CleHistorique)))
        Next
        If _cmbVoies.Items.Count > 0 Then _cmbVoies.SelectedIndex = 0
    End Sub

    ''' <summary>Met à jour la colonne "Dernière valeur" depuis les voies calculées.</summary>
    Public Sub ActualiserValeursGrille()
        For Each row As DataGridViewRow In _dgv.Rows
            Dim id  = If(row.Tag IsNot Nothing, row.Tag.ToString(), "")
            Dim vc  = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
            If vc IsNot Nothing Then
                row.Cells("cValeur").Value =
                    If(vc.EnErreur, "ERR",
                    If(Double.IsNaN(vc.Valeur), "---",
                        vc.Valeur.ToString("F3",
                            System.Globalization.CultureInfo.InvariantCulture) & " " & vc.Unite))
                row.Cells("cValeur").Style.ForeColor =
                    If(vc.EnErreur, Color.FromArgb(190, 30, 30), Color.FromArgb(0, 130, 60))
            End If
        Next
    End Sub

    ' ─── Boutons ─────────────────────────────────────────────────────────────

    Private Sub BtnAjouter_Click(sender As Object, e As EventArgs)
        Dim vc As New VoieCalculee() With {
            .Id   = GestionnaireCalculs.NouvelId(),
            .Nom  = "NouveauCalcul",
            .Unite = "",
            .Expression = ""
        }
        GestCalculs.Voies.Add(vc)
        RemplirGrille()
        _dgv.ClearSelection()
        _dgv.Rows(_dgv.Rows.Count - 1).Selected = True
        ChargerDansEditeur(vc)
    End Sub

    Private Sub BtnSupprimer_Click(sender As Object, e As EventArgs)
        If _dgv.SelectedRows.Count = 0 Then Return
        Dim id  = If(_dgv.SelectedRows(0).Tag IsNot Nothing, _dgv.SelectedRows(0).Tag.ToString(), "")
        Dim vc  = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc Is Nothing Then Return
        Dim rep = MessageBox.Show("Supprimer le calcul « " & vc.Nom & " » ?",
                                  "Confirmer", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If rep <> DialogResult.Yes Then Return
        GestCalculs.Voies.Remove(vc)
        RemplirGrille()
        _indexEdite = -1
        ViderEditeur()
        RaiseEvent CalculsModifies(Me)
    End Sub

    Private Sub BtnMonter_Click(sender As Object, e As EventArgs)
        If _dgv.SelectedRows.Count = 0 Then Return
        Dim idx = _dgv.SelectedRows(0).Index
        If idx <= 0 Then Return
        Dim id = If(_dgv.SelectedRows(0).Tag IsNot Nothing, _dgv.SelectedRows(0).Tag.ToString(), "")
        Dim vc = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc Is Nothing Then Return
        GestCalculs.Voies.Remove(vc)
        GestCalculs.Voies.Insert(idx - 1, vc)
        RemplirGrille()
        _dgv.Rows(idx - 1).Selected = True
    End Sub

    Private Sub BtnDescendre_Click(sender As Object, e As EventArgs)
        If _dgv.SelectedRows.Count = 0 Then Return
        Dim idx = _dgv.SelectedRows(0).Index
        If idx >= _dgv.Rows.Count - 1 Then Return
        Dim id = If(_dgv.SelectedRows(0).Tag IsNot Nothing, _dgv.SelectedRows(0).Tag.ToString(), "")
        Dim vc = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc Is Nothing Then Return
        GestCalculs.Voies.Remove(vc)
        GestCalculs.Voies.Insert(idx + 1, vc)
        RemplirGrille()
        _dgv.Rows(idx + 1).Selected = True
    End Sub

    Private Sub BtnSauver_Click(sender As Object, e As EventArgs)
        AppliquerEditeurVersCalcul()
        GestCalculs.SauverDansConfig(Config)
        Try
            Config.Sauvegarder()
            RaiseEvent StatutChange(Me, "Calculs sauvegardés (" &
                GestCalculs.Voies.Count & " calcul(s)).", False)
            RaiseEvent CalculsModifies(Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnTester_Click(sender As Object, e As EventArgs)
        AppliquerEditeurVersCalcul()
        Dim id = If(_dgv.SelectedRows.Count > 0 AndAlso _dgv.SelectedRows(0).Tag IsNot Nothing, _dgv.SelectedRows(0).Tag.ToString(), "")
        Dim vc = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc Is Nothing Then Return

        If Historique Is Nothing OrElse Historique.ObtenirHorodatages().Count = 0 Then
            ' Test syntaxique uniquement
            Try
                Dim refs = VoieCalculee.ExtraireRefs(vc.Expression)
                Dim valeurs As New Dictionary(Of String, Double)
                For Each r In refs : valeurs(r) = 1.0 : Next
                Dim exprTest = vc.Expression
                For Each kvp In valeurs
                    exprTest = exprTest.Replace("{" & kvp.Key & "}", "1.0")
                Next
                Dim res = EvaluateurExpression.Evaluer(exprTest)
                _lblResultat.Text      = "✔ Syntaxe OK (test avec toutes voies = 1) → " &
                                          res.ToString("G6") & " " & vc.Unite
                _lblResultat.ForeColor = Color.FromArgb(0, 130, 60)
            Catch ex As Exception
                _lblResultat.Text      = "✘ Erreur : " & ex.Message
                _lblResultat.ForeColor = Color.FromArgb(190, 30, 30)
            End Try
            Return
        End If

        vc.Calculer(Historique)
        If vc.EnErreur Then
            _lblResultat.Text      = "✘ " & vc.MessageErreur
            _lblResultat.ForeColor = Color.FromArgb(190, 30, 30)
        Else
            _lblResultat.Text      = "✔ Résultat = " &
                                      vc.Valeur.ToString("G6",
                                          System.Globalization.CultureInfo.InvariantCulture) &
                                      " " & vc.Unite
            _lblResultat.ForeColor = Color.FromArgb(0, 130, 60)
        End If
        ActualiserValeursGrille()
    End Sub

    Private Sub BtnInserer_Click(sender As Object, e As EventArgs)
        If _cmbVoies.SelectedItem Is Nothing Then Return
        Dim item = TryCast(_cmbVoies.SelectedItem, VoieItem)
        If item Is Nothing Then Return
        Dim ref = "{" & item.Cle & "}"
        Dim pos = _txtExpression.SelectionStart
        _txtExpression.Text = _txtExpression.Text.Insert(pos, ref)
        _txtExpression.SelectionStart = pos + ref.Length
        _txtExpression.Focus()
    End Sub

    ' ─── Éditeur ─────────────────────────────────────────────────────────────

    Private Sub Dgv_SelectionChanged(sender As Object, e As EventArgs)
        If _dgv.SelectedRows.Count = 0 Then Return
        Dim id = If(_dgv.SelectedRows(0).Tag IsNot Nothing, _dgv.SelectedRows(0).Tag.ToString(), "")
        Dim vc = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc IsNot Nothing Then ChargerDansEditeur(vc)
    End Sub

    Private Sub Dgv_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        If _dgv.Columns(e.ColumnIndex).Name <> "cActif" Then Return
        Dim id = If(_dgv.Rows(e.RowIndex).Tag IsNot Nothing, _dgv.Rows(e.RowIndex).Tag.ToString(), "")
        Dim vc = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc IsNot Nothing Then
            vc.Active = CBool(If(_dgv.Rows(e.RowIndex).Cells("cActif").Value, False))
            RaiseEvent CalculsModifies(Me)
        End If
    End Sub

    Private Sub ChargerDansEditeur(vc As VoieCalculee)
        _chargementEdit = True
        _indexEdite = GestCalculs.Voies.IndexOf(vc)
        _txtNom.Text        = vc.Nom
        _txtUnite.Text      = vc.Unite
        _txtExpression.Text = vc.Expression
        _numNbMoy.Value     = Math.Max(1, Math.Min(100, vc.NbPointsMoyenne))
        _lblResultat.Text   = ""
        _chargementEdit = False
    End Sub

    Private Sub ViderEditeur()
        _txtNom.Text        = ""
        _txtUnite.Text      = ""
        _txtExpression.Text = ""
        _numNbMoy.Value     = 1
        _lblResultat.Text   = ""
    End Sub

    ''' <summary>Applique les valeurs de l'éditeur vers le calcul sélectionné.</summary>
    ''' <summary>Force la synchronisation UI→objet avant sauvegarde.</summary>
    Public Sub AppliquerEditionCourante()
        AppliquerEditeurVersCalcul()
    End Sub

    Private Sub AppliquerEditeurVersCalcul()
        If _chargementEdit Then Return  ' ne pas écraser pendant un chargement
        ' Utiliser la ligne sélectionnée OU la dernière ligne éditée
        Dim id As String = ""
        If _dgv.SelectedRows.Count > 0 AndAlso _dgv.SelectedRows(0).Tag IsNot Nothing Then
            id = _dgv.SelectedRows(0).Tag.ToString()
        ElseIf _indexEdite >= 0 AndAlso _indexEdite < _dgv.Rows.Count Then
            If _dgv.Rows(_indexEdite).Tag IsNot Nothing Then
                id = _dgv.Rows(_indexEdite).Tag.ToString()
            End If
        End If
        If id = "" Then Return
        Dim vc = GestCalculs.Voies.FirstOrDefault(Function(v) v.Id = id)
        If vc Is Nothing Then Return
        vc.Nom              = _txtNom.Text.Trim()
        vc.Unite            = _txtUnite.Text.Trim()
        vc.Expression       = _txtExpression.Text.Trim()
        vc.NbPointsMoyenne  = CInt(_numNbMoy.Value)
        ' Mettre à jour la grille
        Dim row = _dgv.SelectedRows(0)
        row.Cells("cNom").Value        = vc.Nom
        row.Cells("cUnite").Value      = vc.Unite
        row.Cells("cExpression").Value = vc.Expression
        row.Cells("cNbMoy").Value      = vc.NbPointsMoyenne.ToString()
    End Sub

    ' ─── Helpers ─────────────────────────────────────────────────────────────

    Private Shared Sub StylerBouton(btn As Button, texte As String, couleur As Color)
        btn.Text      = texte
        btn.BackColor = couleur
        btn.ForeColor = Color.White
        btn.FlatStyle = FlatStyle.Flat
        btn.Height    = 28
        btn.AutoSize  = True
        btn.Margin    = New Padding(0, 0, 6, 0)
    End Sub

End Class

' ─── Classe helper pour la liste déroulante ───────────────────────────────────

Public Class VoieItem
    Public Property Cle     As String
    Public Property Libelle As String

    Public Sub New(cle As String, libelle As String)
        Me.Cle     = cle
        Me.Libelle = libelle
    End Sub

    Public Overrides Function ToString() As String
        Return Libelle
    End Function
End Class
