Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Diagnostics
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

'''<summary>
''' Serialise la configuration de l'essai en JSON et appelle le script Python
''' generer_rapport.py pour produire un PDF de rapport.
''' </summary>
Public Class GenerateurRapport

    Public Property Gestionnaire  As GestionnaireMultiCentrale
    Public Property GestCalculs   As GestionnaireCalculs
    Public Property Acquisition   As MoteurAcquisition
    Public Property OngletCSV     As OngletCSV
    Public Property ChronoActif    As Boolean = False
    Public Property ArreterAcqFin  As Boolean = False
    ' Données pré-capturées depuis le thread UI — remplacent DgvEtapes/DgvRegles/OngletCSV
    Public Property FormatValeur      As String = "F3"
    Public Property LibelleUniteDuree As String = "s"
    Public Property EtapesChronoData  As List(Of Dictionary(Of String, String)) = Nothing
    Public Property ReglesChronoData  As List(Of Dictionary(Of String, String)) = Nothing
    ' Conservées pour compatibilité descendante (non lues depuis un thread background)
    Public Property DgvEtapes      As DataGridView = Nothing
    Public Property DgvRegles      As DataGridView = Nothing
    Public Property CheminScript  As String = ""
    Public Property CheminPython   As String = ""   ' chemin explicite vers python.exe si besoin
    Public Property Operateur      As String = "Adrien"
    Public Property Laboratoire    As String = "IRDL PTR4 — Lorient"
    Public Property Projet         As String = ""
    Public Property Notes          As String = ""
    Public Property CheminLogo     As String = ""
    Public Property NomPolice      As String = "Helvetica"
    Public Property CheminGraphique As String = ""   ' chemin PNG du graphique exporté

    Public Function Generer(cheminPDF As String) As String
        Try
            Dim d As New Dictionary(Of String, Object)

            d("date_debut")  = If(Gestionnaire IsNot Nothing AndAlso
                                  Gestionnaire.HeureDepart <> DateTime.MinValue,
                                  Gestionnaire.HeureDepart.ToString("yyyy-MM-dd HH:mm:ss"),
                                  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            d("chemin_csv")  = If(Acquisition IsNot Nothing, Acquisition.CheminCSV, "")
            d("operateur")   = Operateur
            d("labo")        = Laboratoire
            d("projet")      = Projet
            d("notes")       = Notes
            d("chemin_logo") = CheminLogo
            d("police")      = NomPolice
            d("chemin_graphique") = CheminGraphique
            d("mode")        = If(Acquisition IsNot Nothing AndAlso
                                  Acquisition.ModeSim <> MoteurAcquisition.ModeSimulation.Desactive,
                                  "Simulation", "Acquisition multi-centrale")

            ' Centrales
            Dim centrales As New List(Of Dictionary(Of String, Object))
            If Gestionnaire IsNot Nothing Then
                For Each c In Gestionnaire.Centrales
                    Dim dc As New Dictionary(Of String, Object)
                    dc("nom")    = c.NomAffiche
                    dc("ip")     = c.IPAddress
                    dc("port")   = c.Port.ToString()
                    dc("statut") = If(c.EstConnectee, "Connectee", "Non connectee")
                    dc("modele") = "Keithley 2700/2701"
                    centrales.Add(dc)
                Next
            End If
            d("centrales") = centrales

            ' Voies
            Dim voies As New List(Of Dictionary(Of String, Object))
            If Gestionnaire IsNot Nothing Then
                For Each c In Gestionnaire.Centrales
                    For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                        Dim dv As New Dictionary(Of String, Object)
                        dv("centrale")     = c.NomAffiche
                        dv("numero")       = v.Numero.ToString()
                        dv("nom")          = v.Nom
                        dv("unite")        = v.Unite
                        dv("type")         = v.Type.ToString()
                        dv("alarme_basse") = If(Double.IsNaN(v.SeuilBas),  "---", v.SeuilBas.ToString("F2"))
                        dv("alarme_haute") = If(Double.IsNaN(v.SeuilHaut), "---", v.SeuilHaut.ToString("F2"))
                        dv("secu_debit")   = If(v.SurveillanceDebit, "Oui", "")
                        voies.Add(dv)
                    Next
                Next
            End If
            d("voies") = voies

            ' Sorties
            Dim sorties As New List(Of Dictionary(Of String, Object))
            If Gestionnaire IsNot Nothing Then
                For Each c In Gestionnaire.Centrales
                    For Each s In c.Voies.SortiesActives()
                        Dim ds As New Dictionary(Of String, Object)
                        ds("centrale")   = c.NomAffiche
                        ds("numero")     = s.Numero.ToString()
                        ds("nom")        = s.Nom
                        ds("mode")       = s.LibelleMode
                        ds("amplitude")  = s.Amplitude.ToString("F1")
                        ds("secu_debit") = If(s.SecuriteDebit, "Oui", "")
                        sorties.Add(ds)
                    Next
                Next
            End If
            d("sorties") = sorties

            ' Acquisition
            Dim acq As New Dictionary(Of String, Object)
            acq("intervalle")    = If(Acquisition IsNot Nothing,
                                     (Acquisition.IntervalleMsec \ 1000).ToString() & " s", "—")
            acq("chemin_csv")    = If(Acquisition IsNot Nothing, Acquisition.CheminCSV, "—")
            acq("format_valeur") = FormatValeur
            acq("unite_duree")   = LibelleUniteDuree
            acq("simulation")    = If(Acquisition IsNot Nothing AndAlso
                                     Acquisition.ModeSim <> MoteurAcquisition.ModeSimulation.Desactive,
                                     "Oui", "Non")
            d("acquisition") = acq

            ' Voies calculees
            Dim calcs As New List(Of Dictionary(Of String, Object))
            If GestCalculs IsNot Nothing Then
                For Each vc In GestCalculs.Voies.Where(Function(v) v.Active)
                    Dim dc As New Dictionary(Of String, Object)
                    dc("nom")        = vc.Nom
                    dc("unite")      = vc.Unite
                    dc("expression") = vc.Expression
                    dc("nb_moy")     = vc.NbPointsMoyenne.ToString()
                    calcs.Add(dc)
                Next
            End If
            d("calculs") = calcs

            ' Mapping cle_historique → nom de voie (pour les expressions dans le rapport)
            Dim nomsVoiesMap As New Dictionary(Of String, Object)
            If Gestionnaire IsNot Nothing Then
                For Each cv In Gestionnaire.Centrales
                    For Each vv In cv.Voies.Voies.Where(Function(x) x.Active)
                        Dim cle = HistoriqueMultiCentrale.CleVoie(cv.Numero, vv.Numero)
                        nomsVoiesMap(cle) = vv.Nom
                    Next
                Next
            End If
            If GestCalculs IsNot Nothing Then
                For Each vc In GestCalculs.Voies.Where(Function(v) v.Active)
                    nomsVoiesMap(vc.CleHistorique) = vc.Nom
                Next
            End If
            d("noms_voies_map") = nomsVoiesMap

            ' Chronogramme — données pré-capturées depuis le thread UI (thread-safe)
            If ChronoActif AndAlso EtapesChronoData IsNot Nothing Then
                Dim chrono As New Dictionary(Of String, Object)
                chrono("duree_totale")    = ""
                chrono("boucler")         = "Oui"
                chrono("arreter_acq_fin") = If(ArreterAcqFin, "Oui", "Non")

                Dim etapes As New List(Of Dictionary(Of String, Object))
                For Each rowData In EtapesChronoData
                    Dim de As New Dictionary(Of String, Object)
                    de("nom")   = If(rowData.ContainsKey("nom"),   rowData("nom"),   "")
                    de("duree") = If(rowData.ContainsKey("duree"), rowData("duree"), "")
                    ' Collecter les états des sorties (clés "sortie_bool_*" et "sortie_anal_*")
                    Dim detailsSorties As New List(Of String)
                    For Each kv In rowData
                        If kv.Key.StartsWith("sortie_bool_") OrElse kv.Key.StartsWith("sortie_anal_") Then
                            detailsSorties.Add(kv.Value)
                        End If
                    Next
                    de("etats_sorties") = String.Join(" | ", detailsSorties)
                    etapes.Add(de)
                Next
                chrono("etapes") = etapes

                Dim regles As New List(Of Dictionary(Of String, Object))
                If ReglesChronoData IsNot Nothing Then
                    For Each rowData In ReglesChronoData
                        Dim dr As New Dictionary(Of String, Object)
                        dr("voie_nom")   = If(rowData.ContainsKey("rVoie"),   rowData("rVoie"),   "")
                        dr("operateur")  = If(rowData.ContainsKey("rOp"),     rowData("rOp"),     "")
                        dr("seuil")      = If(rowData.ContainsKey("rVal"),    rowData("rVal"),    "")
                        dr("sortie_nom") = If(rowData.ContainsKey("rRelais"), rowData("rRelais"), "")
                        dr("action")     = If(rowData.ContainsKey("rAction"), rowData("rAction"), "")
                        regles.Add(dr)
                    Next
                End If
                chrono("regles") = regles
                d("chronogramme") = chrono
            End If

            d("peripheriques") = New List(Of Dictionary(Of String, Object))

            ' Serialiser
            Dim json = ToJson(d)
            Dim cheminJSON = Path.ChangeExtension(cheminPDF, ".json")
            File.WriteAllText(cheminJSON, json, New System.Text.UTF8Encoding(False))

            ' ── Chercher generer_rapport.exe ou .py ──────────────────────────
            Dim dossiers As New List(Of String)
            Dim base As String = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)
            Dim courant As String = base
            For iter As Integer = 0 To 5
                If courant = "" OrElse courant Is Nothing Then Exit For
                dossiers.Add(courant)
                Try
                    Dim parent = Directory.GetParent(courant)
                    If parent Is Nothing Then Exit For
                    courant = parent.FullName
                Catch
                    Exit For
                End Try
            Next

            Dim exeRapport As String = ""
            Dim scriptPath As String = ""
            If CheminScript <> "" Then
                If CheminScript.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) Then
                    exeRapport = CheminScript
                Else
                    scriptPath = CheminScript
                End If
            Else
                For Each doss In dossiers
                    Dim candidatExe = Path.Combine(doss, "generer_rapport.exe")
                    If File.Exists(candidatExe) Then
                        exeRapport = candidatExe : Exit For
                    End If
                Next
                If exeRapport = "" Then
                    For Each doss In dossiers
                        Dim candidatPy = Path.Combine(doss, "generer_rapport.py")
                        If File.Exists(candidatPy) Then
                            scriptPath = candidatPy : Exit For
                        End If
                    Next
                End If
                If exeRapport = "" AndAlso scriptPath = "" Then
                    Dim msgErr As String =
                        "Ni generer_rapport.exe ni generer_rapport.py introuvable." & vbNewLine &
                        "Copiez l'un de ces fichiers dans :" & vbNewLine
                    For i As Integer = 0 To Math.Min(2, dossiers.Count - 1)
                        msgErr &= "  - " & dossiers(i) & vbNewLine
                    Next
                    Throw New Exception(msgErr)
                End If
            End If

            ' ── Lancer la génération ──────────────────────────────────────────
            Dim psi As ProcessStartInfo
            If exeRapport <> "" Then
                ' Mode autonome : generer_rapport.exe (aucun Python requis)
                psi = New ProcessStartInfo(exeRapport) With {
                    .Arguments              = String.Format("""{0}"" ""{1}""",
                                                 cheminJSON, cheminPDF),
                    .UseShellExecute        = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError  = True,
                    .CreateNoWindow         = True
                }
            Else
                ' Mode script : python generer_rapport.py
                Dim exePython = TrouverPython()
                If exePython = "" Then
                    Throw New Exception(
                        "Python introuvable et generer_rapport.exe absent." & vbNewLine &
                        "Sur la machine cible, copiez generer_rapport.exe dans le dossier de Thermopilot.exe.")
                End If
                psi = New ProcessStartInfo(exePython) With {
                    .Arguments              = String.Format("""{0}"" ""{1}"" ""{2}""",
                                                 scriptPath, cheminJSON, cheminPDF),
                    .UseShellExecute        = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError  = True,
                    .CreateNoWindow         = True
                }
            End If

            Using proc = Process.Start(psi)
                Dim erreur = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                If proc.ExitCode <> 0 Then
                    Throw New Exception("Génération rapport : " & erreur)
                End If
            End Using
            Try : File.Delete(cheminJSON) : Catch : End Try
            Return cheminPDF

        Catch ex As Exception
            RaiseEvent ErreurGeneration(Me, ex.Message)
            Return ""
        End Try
    End Function

    Public Event ErreurGeneration(sender As Object, message As String)

    Private Shared Function CellStr(row As DataGridViewRow, col As String) As String
        If row.Cells(col) Is Nothing OrElse row.Cells(col).Value Is Nothing Then Return ""
        Return row.Cells(col).Value.ToString()
    End Function

    ' Détection de l'exécutable Python
    Private Function TrouverPython() As String
        ' 0. Utiliser le chemin explicite si fourni
        If CheminPython <> "" AndAlso File.Exists(CheminPython) Then Return CheminPython

        ' 1. Chercher dans les noms courants dans le PATH
        For Each nom In {"python3", "python", "py"}
            Try
                Dim psi As New ProcessStartInfo(nom) With {
                    .Arguments              = "--version",
                    .UseShellExecute        = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError  = True,
                    .CreateNoWindow         = True
                }
                Using proc = Process.Start(psi)
                    proc.WaitForExit()
                    If proc.ExitCode = 0 Then Return nom
                End Using
            Catch
            End Try
        Next

        ' 2. Chercher dans les emplacements d'installation standard Windows
        Dim chemins As String() = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Programs", "Python", "Python311", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Programs", "Python", "Python310", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Programs", "Python", "Python39", "python.exe"),
            "C:\Python312\python.exe",
            "C:\Python311\python.exe",
            "C:\Python310\python.exe",
            "C:\Python39\python.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "Python312", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "Python311", "python.exe")
        }
        For Each chemin In chemins
            If File.Exists(chemin) Then Return chemin
        Next

        ' 3. Chercher py.exe (launcher Windows)
        Dim pyLauncher = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "py.exe")
        If File.Exists(pyLauncher) Then Return pyLauncher

        Return ""
    End Function

    ' Serialiseur JSON minimal sans dependance externe
    Private Shared Function ToJson(obj As Object) As String
        If obj Is Nothing Then Return "null"

        If TypeOf obj Is Dictionary(Of String, Object) Then
            Dim d = DirectCast(obj, Dictionary(Of String, Object))
            Dim parts As New List(Of String)
            For Each kv In d
                parts.Add("""" & kv.Key & """:" & ToJson(kv.Value))
            Next
            Return "{" & String.Join(",", parts) & "}"
        End If

        If TypeOf obj Is List(Of Dictionary(Of String, Object)) Then
            Dim l = DirectCast(obj, List(Of Dictionary(Of String, Object)))
            Return "[" & String.Join(",", l.Select(Function(x) ToJson(x))) & "]"
        End If

        If TypeOf obj Is Boolean Then
            Return If(CBool(obj), "true", "false")
        End If

        If TypeOf obj Is Integer OrElse TypeOf obj Is Long OrElse
           TypeOf obj Is Double OrElse TypeOf obj Is Single Then
            Return Convert.ToString(obj,
                System.Globalization.CultureInfo.InvariantCulture)
        End If

        Dim s = obj.ToString() _
            .Replace("\", "\\") _
            .Replace("""", "\""") _
            .Replace(Chr(13) & Chr(10), " ") _
            .Replace(Chr(10), " ") _
            .Replace(Chr(13), " ")
        Return """" & s & """"
    End Function

End Class
