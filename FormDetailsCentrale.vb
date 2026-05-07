Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

''' <summary>
''' Fenêtre modale affichant les commandes SCPI utilisées par la centrale.
''' Structure fixe, seules les valeurs paramétrables sont éditables.
''' Bouton "Valeurs par défaut" pour restaurer.
''' </summary>
Public Class FormDetailsCentrale
    Inherits Form

    Private _typeCentrale  As TypeCentrale
    Private _commandes     As List(Of CommandeSCPI)
    Private _dgv           As New DataGridView()
    Private _btnDefaut     As New Button()
    Private _btnFermer     As New Button()
    Private _lblTitre      As New Label()

    ' ─── Constructeur ────────────────────────────────────────────────────────

    Public Sub New(typeCentrale As TypeCentrale,
                   Optional commandesPersonnalisees As List(Of CommandeSCPI) = Nothing)
        _typeCentrale = typeCentrale
        _commandes = If(commandesPersonnalisees IsNot Nothing,
                        commandesPersonnalisees,
                        CommandeSCPI.ParDefaut)

        Me.Text            = "Détails — " & LibelleCentrale(typeCentrale)
        Me.Size            = New Size(860, 620)
        Me.MinimumSize     = New Size(700, 400)
        Me.StartPosition   = FormStartPosition.CenterParent
        Me.Font            = New Font("Segoe UI", 9)
        Me.BackColor       = Color.FromArgb(245, 247, 252)

        ConstruireUI()
        RemplirGrille()
    End Sub

    ' ─── Résultat ─────────────────────────────────────────────────────────────

    ''' <summary>Commandes après modification par l'utilisateur.</summary>
    Public ReadOnly Property CommandesResultat As List(Of CommandeSCPI)
        Get
            Return _commandes
        End Get
    End Property

    ' ─── Construction ─────────────────────────────────────────────────────────

    Private Sub ConstruireUI()
        ' En-tête
        _lblTitre.Text      = "COMMANDES SCPI — " & LibelleCentrale(_typeCentrale).ToUpper()
        _lblTitre.Dock      = DockStyle.Top
        _lblTitre.Height    = 36
        _lblTitre.Font      = New Font("Segoe UI", 10, FontStyle.Bold)
        _lblTitre.ForeColor = Color.FromArgb(40, 80, 160)
        _lblTitre.Padding   = New Padding(10, 8, 0, 0)

        Dim lblNote As New Label() With {
            .Text      = "Les cellules en blanc sont modifiables. Celles en gris sont fixes." &
                         "  Les variables {VOIES}, {TC}, {V}, {SORTIE} sont remplacées automatiquement à l'exécution.",
            .Dock      = DockStyle.Top,
            .Height    = 20,
            .Font      = New Font("Segoe UI", 8, FontStyle.Italic),
            .ForeColor = Color.Gray,
            .Padding   = New Padding(10, 0, 0, 0)
        }

        ' Grille
        _dgv.Dock                  = DockStyle.Fill
        _dgv.AllowUserToAddRows    = False
        _dgv.AllowUserToDeleteRows = False
        _dgv.RowHeadersVisible     = False
        _dgv.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgv.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2
        _dgv.Font                  = New Font("Segoe UI", 9)
        _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgv.GridColor             = Color.FromArgb(210, 215, 230)
        _dgv.CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal

        _dgv.Columns.AddRange({
            New DataGridViewTextBoxColumn() With {
                .Name = "cCat", .HeaderText = "Catégorie", .Width = 180, .ReadOnly = True
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cCmd", .HeaderText = "Commande SCPI", .Width = 240
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cDesc", .HeaderText = "Rôle / Description", .ReadOnly = True
            }
        })

        _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 230, 248)
        _dgv.ColumnHeadersDefaultCellStyle.Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        _dgv.EnableHeadersVisualStyles = False

        AddHandler _dgv.CellValueChanged,     AddressOf Dgv_CellValueChanged
        AddHandler _dgv.CellFormatting,       AddressOf Dgv_CellFormatting
        AddHandler _dgv.DefaultValuesNeeded,  Nothing

        ' Barre de boutons
        Dim pnlBas As New Panel() With {
            .Dock   = DockStyle.Bottom,
            .Height = 46,
            .Padding = New Padding(8, 8, 8, 0)
        }
        Dim fl As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight}

        _btnDefaut.Text      = "↺ Valeurs par défaut"
        _btnDefaut.BackColor = Color.FromArgb(140, 80, 20)
        _btnDefaut.ForeColor = Color.White
        _btnDefaut.FlatStyle = FlatStyle.Flat
        _btnDefaut.Height    = 28
        _btnDefaut.AutoSize  = True

        _btnFermer.Text      = "✔ Fermer"
        _btnFermer.BackColor = Color.FromArgb(40, 110, 175)
        _btnFermer.ForeColor = Color.White
        _btnFermer.FlatStyle = FlatStyle.Flat
        _btnFermer.Height    = 28
        _btnFermer.Width     = 100
        _btnFermer.Margin    = New Padding(12, 0, 0, 0)

        fl.Controls.AddRange({_btnDefaut, _btnFermer})
        pnlBas.Controls.Add(fl)

        Me.Controls.Add(_dgv)
        Me.Controls.Add(pnlBas)
        Me.Controls.Add(lblNote)
        Me.Controls.Add(_lblTitre)

        AddHandler _btnDefaut.Click, AddressOf BtnDefaut_Click
        AddHandler _btnFermer.Click, Sub(s, e) Me.Close()
    End Sub

    ' ─── Remplissage ──────────────────────────────────────────────────────────

    Private Sub RemplirGrille()
        _dgv.Rows.Clear()
        Dim catCourante As CommandeSCPI.CategorieCommande = CType(-1, CommandeSCPI.CategorieCommande)

        For Each cmd In _commandes
            ' Ligne de séparation de catégorie
            If cmd.Categorie <> catCourante Then
                catCourante = cmd.Categorie
                Dim idxSep = _dgv.Rows.Add(
                    "── " & CommandeSCPI.LibelleCategorie(cmd.Categorie).ToUpper() & " ──",
                    "", "")
                _dgv.Rows(idxSep).DefaultCellStyle.BackColor = Color.FromArgb(215, 225, 245)
                _dgv.Rows(idxSep).DefaultCellStyle.Font      = New Font("Segoe UI", 8.5, FontStyle.Bold)
                _dgv.Rows(idxSep).DefaultCellStyle.ForeColor = Color.FromArgb(40, 60, 120)
                _dgv.Rows(idxSep).ReadOnly = True
                _dgv.Rows(idxSep).Tag      = Nothing   ' pas de commande associée
            End If

            Dim idx = _dgv.Rows.Add("", cmd.Commande, cmd.Description)
            _dgv.Rows(idx).Tag = cmd
            _dgv.Rows(idx).Cells("cCmd").ReadOnly = Not cmd.EstModifiable
        Next
    End Sub

    Private Sub Dgv_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = _dgv.Rows(e.RowIndex)
        If row.Tag Is Nothing Then Return   ' ligne séparateur
        Dim cmd = TryCast(row.Tag, CommandeSCPI)
        If cmd Is Nothing Then Return

        If e.ColumnIndex = _dgv.Columns("cCmd").Index Then
            If cmd.EstModifiable Then
                e.CellStyle.BackColor = Color.White
                e.CellStyle.ForeColor = Color.Black
            Else
                e.CellStyle.BackColor = Color.FromArgb(240, 240, 245)
                e.CellStyle.ForeColor = Color.FromArgb(80, 90, 110)
            End If
        End If
    End Sub

    Private Sub Dgv_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = _dgv.Rows(e.RowIndex)
        Dim cmd = TryCast(row.Tag, CommandeSCPI)
        If cmd Is Nothing OrElse Not cmd.EstModifiable Then Return
        cmd.Commande = If(row.Cells("cCmd").Value IsNot Nothing,
                          row.Cells("cCmd").Value.ToString(), "")
    End Sub

    Private Sub BtnDefaut_Click(sender As Object, e As EventArgs)
        Dim rep = MessageBox.Show(
            "Restaurer toutes les commandes aux valeurs par défaut ?",
            "Valeurs par défaut",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If rep = DialogResult.Yes Then
            _commandes = CommandeSCPI.ParDefaut
            RemplirGrille()
        End If
    End Sub

    ' ─── Helper ───────────────────────────────────────────────────────────────

    Public Shared Function LibelleCentrale(t As TypeCentrale) As String
        Select Case t
            Case TypeCentrale.Keithley2701Ethernet : Return "Keithley 2701 Ethernet"
            Case Else                               : Return "Autre"
        End Select
    End Function

End Class
