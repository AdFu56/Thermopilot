Imports System.IO
Imports System.Collections.Generic
Imports System
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Linq

''' <summary>
''' Gestion du fichier de configuration .ini unique.
''' </summary>
Public Class ConfigManager

    Public Shared Property CheminFichier As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Thermopilot", "config.ini")

    Public Shared ReadOnly Property CheminPeripheriques As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Thermopilot", "peripheriques.ini")
        End Get
    End Property

    Private _data As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)

    ' ─── Sections ─────────────────────────────────────────────────────────────

    Public Const SEC_CONNEXION As String = "Connexion"
    Public Const SEC_VOIES     As String = "Voies"
    Public Const SEC_CSV       As String = "CSV"
    Public Const SEC_CHRONO    As String = "Chronogramme"
    Public Const SEC_RELAIS    As String = "Relais"
    Public Const SEC_PERIPH    As String = "Peripheriques"
    Public Const SEC_SYSTEME   As String = "Systeme"

    ' ─── Lecture / Écriture ───────────────────────────────────────────────────

    Public Function Get_(section As String, cle As String,
                         Optional defaut As String = "") As String
        If _data.ContainsKey(section) AndAlso _data(section).ContainsKey(cle) Then
            Return _data(section)(cle)
        End If
        Return defaut
    End Function

    Public Function GetInt(section As String, cle As String,
                           Optional defaut As Integer = 0) As Integer
        Dim v As Integer
        If Integer.TryParse(Get_(section, cle), v) Then Return v
        Return defaut
    End Function

    Public Function GetDouble(section As String, cle As String,
                              Optional defaut As Double = 0.0) As Double
        Dim v As Double
        If Double.TryParse(Get_(section, cle),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, v) Then Return v
        Return defaut
    End Function

    Public Function GetBool(section As String, cle As String,
                            Optional defaut As Boolean = False) As Boolean
        Dim s = Get_(section, cle, "")
        If s = "" Then Return defaut
        Return s.Equals("true", StringComparison.OrdinalIgnoreCase) OrElse s = "1"
    End Function

    Public Sub Set_(section As String, cle As String, valeur As Object)
        If Not _data.ContainsKey(section) Then
            _data(section) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        End If
        Dim str As String
        If TypeOf valeur Is Double OrElse TypeOf valeur Is Single Then
            str = Convert.ToDouble(valeur).ToString(System.Globalization.CultureInfo.InvariantCulture)
        ElseIf TypeOf valeur Is Boolean Then
            str = If(CBool(valeur), "true", "false")
        Else
            str = valeur.ToString()
        End If
        _data(section)(cle) = str
    End Sub

    ' ─── Chargement ───────────────────────────────────────────────────────────

    Public Sub Charger()
        _data.Clear()
        If Not File.Exists(CheminFichier) Then Return
        Dim sectionCourante As String = ""
        For Each ligne In File.ReadAllLines(CheminFichier, System.Text.Encoding.UTF8)
            Dim l = ligne.Trim()
            If l = "" OrElse l.StartsWith(";") OrElse l.StartsWith("#") Then Continue For
            If l.StartsWith("[") AndAlso l.EndsWith("]") Then
                sectionCourante = l.Substring(1, l.Length - 2).Trim()
                If Not _data.ContainsKey(sectionCourante) Then
                    _data(sectionCourante) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                End If
            ElseIf sectionCourante <> "" Then
                Dim idx As Integer = l.IndexOf("="c)
                If idx > 0 Then
                    Dim cle = l.Substring(0, idx).Trim()
                    Dim val = l.Substring(idx + 1).Trim()
                    _data(sectionCourante)(cle) = val
                End If
            End If
        Next
    End Sub

    ' ─── Sauvegarde ───────────────────────────────────────────────────────────

    Public Sub Sauvegarder()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(CheminFichier))
            Dim lignes As New List(Of String)
            lignes.Add("; Configuration Thermopilot — " &
                       DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lignes.Add("")
            For Each kvpSection In _data
                lignes.Add("[" & kvpSection.Key & "]")
                For Each kvp In kvpSection.Value
                    lignes.Add(kvp.Key & "=" & kvp.Value)
                Next
                lignes.Add("")
            Next
            File.WriteAllLines(CheminFichier, lignes, System.Text.Encoding.UTF8)
        Catch ex As Exception
            Throw New IOException(
                "Impossible de sauvegarder la configuration : " & ex.Message, ex)
        End Try
    End Sub

    ''' <summary>Charge depuis un chemin explicite et met à jour CheminFichier.</summary>
    Public Sub ChargerDepuis(chemin As String)
        CheminFichier = chemin
        Charger()
    End Sub

    ''' <summary>Sauvegarde vers un chemin explicite et met à jour CheminFichier.</summary>
    Public Sub SauvegarderVers(chemin As String)
        CheminFichier = chemin
        Sauvegarder()
    End Sub

    ''' <summary>Sauvegarde vers un chemin explicite SANS modifier CheminFichier.</summary>
    Public Sub SauvegarderDans(chemin As String)
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(chemin))
            Dim lignes As New List(Of String)
            lignes.Add("; Configuration Thermopilot — " &
                       DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lignes.Add("")
            For Each kvpSection In _data
                lignes.Add("[" & kvpSection.Key & "]")
                For Each kvp In kvpSection.Value
                    lignes.Add(kvp.Key & "=" & kvp.Value)
                Next
                lignes.Add("")
            Next
            File.WriteAllLines(chemin, lignes, System.Text.Encoding.UTF8)
        Catch ex As Exception
            Throw New IOException(
                "Impossible de sauvegarder : " & ex.Message, ex)
        End Try
    End Sub

    ' ─── Valeurs par défaut ───────────────────────────────────────────────────

    Public Sub AppliquerDefauts()
        ' Connexion TCP/IP — multi-centrale
        If Get_(SEC_CONNEXION, "NbCentrales") = "" Then Set_(SEC_CONNEXION, "NbCentrales", "1")
        ' Valeurs par défaut pour la centrale 1
        If Get_("Centrale1", "Nom") = ""       Then Set_("Centrale1", "Nom",       "Centrale 1")
        If Get_("Centrale1", "IPAddress") = "" Then Set_("Centrale1", "IPAddress", "192.168.0.3")
        If Get_("Centrale1", "Port") = ""      Then Set_("Centrale1", "Port",      "1394")
        If Get_("Centrale1", "Timeout") = ""   Then Set_("Centrale1", "Timeout",   "3000")

        ' Voies — cartes
        If Get_(SEC_VOIES, "NbCartes") = ""       Then Set_(SEC_VOIES, "NbCartes", "1")
        If Get_(SEC_VOIES, "TypeTC") = ""         Then Set_(SEC_VOIES, "TypeTC", "K")

        ' CSV
        If Get_(SEC_CSV, "Dossier") = "" Then
            Set_(SEC_CSV, "Dossier",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                             "Thermopilot"))
        End If
        If Get_(SEC_CSV, "Prefixe") = ""          Then Set_(SEC_CSV, "Prefixe", "Mesures_")
        If Get_(SEC_CSV, "Intervalle") = ""       Then Set_(SEC_CSV, "Intervalle", "5")

        ' Relais
        If Get_(SEC_RELAIS, "SeuilDebit") = ""    Then Set_(SEC_RELAIS, "SeuilDebit", "5.0")

        ' Chronogramme
        If Get_(SEC_CHRONO, "DureeCycle") = ""    Then Set_(SEC_CHRONO, "DureeCycle", "24")
        If Get_(SEC_CHRONO, "UniteDuree") = ""    Then Set_(SEC_CHRONO, "UniteDuree", "2")
        If Get_(SEC_CHRONO, "Boucler") = ""       Then Set_(SEC_CHRONO, "Boucler", "true")
    End Sub

End Class
