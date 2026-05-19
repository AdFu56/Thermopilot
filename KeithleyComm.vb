Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports Microsoft.VisualBasic

Public Class KeithleyComm
    Implements IDisposable

    Private _client As TcpClient
    Private _stream As NetworkStream
    Private _lock As New Object()

    ' Propriétés pour la compatibilité avec l'interface
    Public Property IPAddress As String = "192.168.0.3"
    Public Property Port As Integer = 1394
    Public Property IsConnected As Boolean = False

    ' On garde ces propriétés pour éviter les erreurs BC30456 dans OngletConnexion
    ' même si elles ne sont plus utilisées pour le TCP
    Public Property PortName As String = ""
    Public Property BaudRate As Integer = 9600
    Public Property ReadTimeout As Integer
        Get
            Return _readTimeout
        End Get
        Set(value As Integer)
            _readTimeout = value
            If _stream IsNot Nothing Then
                Try : _stream.ReadTimeout = value : Catch : End Try
            End If
        End Set
    End Property
    Private _readTimeout As Integer = 3000

    Public Function Connect() As Boolean
        Try
            If IsConnected Then Disconnect()
            _client = New TcpClient()
            Dim result = _client.BeginConnect(IPAddress, Port, Nothing, Nothing)
            Dim success = result.AsyncWaitHandle.WaitOne(_readTimeout)

            If Not success Then Return False

            _client.EndConnect(result)
            _stream = _client.GetStream()
            _stream.ReadTimeout = _readTimeout
            IsConnected = True

            ' Initialisation standard Keithley
            SendCommand("*RST")
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Sub SendCommand(cmd As String)
        If Not IsConnected OrElse _stream Is Nothing Then Return
        SyncLock _lock
            SendRaw(cmd)
        End SyncLock
    End Sub

    ' Envoi brut sans lock — utilisé en interne par Query qui locke déjà
    Private Sub SendRaw(cmd As String)
        Try
            ' DAQ6510 (port 5025) attend LF ou CRLF — vbCrLf est compatible
            ' avec le 2701 (port 1394) qui accepte CR, CRLF et LF
            Dim data As Byte() = Encoding.ASCII.GetBytes(cmd & vbCrLf)
            _stream.Write(data, 0, data.Length)
        Catch
            IsConnected = False
        End Try
    End Sub

    ' Délai en ms entre l'envoi d'une commande et la lecture de la réponse.
    ' Augmenter si ERR apparaît sur les voies à partir d'un certain rang.
    Public Property DelaiLectureMs As Integer = 100

    Public Function Query(cmd As String) As String
        If Not IsConnected OrElse _stream Is Nothing Then Return ""
        SyncLock _lock
            Try
                SendRaw(cmd)

                ' Attendre que la centrale ait le temps de préparer la réponse
                If DelaiLectureMs > 0 Then Thread.Sleep(DelaiLectureMs)

                ' Lecture robuste multi-paquets :
                ' Le Keithley peut envoyer la réponse en plusieurs fragments TCP.
                ' On lit en boucle jusqu'à ce que les données soient stables.
                Dim sb As New StringBuilder()
                Dim buf(4095) As Byte
                Dim deadline = DateTime.Now.AddMilliseconds(_stream.ReadTimeout)

                Do
                    If _stream.DataAvailable Then
                        Dim n = _stream.Read(buf, 0, buf.Length)
                        If n > 0 Then sb.Append(Encoding.ASCII.GetString(buf, 0, n))
                    Else
                        ' Pas de données disponibles — si on a déjà quelque chose, on attend
                        ' brièvement pour voir si un autre paquet arrive
                        If sb.Length > 0 Then
                            Thread.Sleep(20)
                            If Not _stream.DataAvailable Then Exit Do
                        Else
                            Thread.Sleep(10)
                        End If
                    End If
                    If DateTime.Now > deadline Then Exit Do
                Loop

                Return sb.ToString().Trim()
            Catch
                Return ""
            End Try
        End SyncLock
    End Function

    Public Sub ConfigurerScan(voiesTemp As String, voiesDebit As String, typeTC As String)
        ' Séquence calquée sur l'ancien KI2701config() fonctionnel

        SendCommand("*RST")
        SendCommand("FORM:ELEM READ")   ' Format : valeurs uniquement
        SendCommand("TRAC:CLE")         ' Vider le buffer

        ' Configuration des thermocouples
        If Not String.IsNullOrEmpty(voiesTemp) Then
            SendCommand(String.Format("UNIT:TEMP C,(@{0})", voiesTemp))
            SendCommand(String.Format("FUNC 'TEMP',(@{0})", voiesTemp))
            SendCommand(String.Format("TEMP:TRAN TC,(@{0})", voiesTemp))
            SendCommand(String.Format("TEMP:TC:TYPE {0},(@{1})", typeTC, voiesTemp))
            SendCommand(String.Format("SENS:TEMP:APER 0.05,(@{0})", voiesTemp))
        End If

        ' Configuration des voies tension DC (débitmètres, capteurs 4-20mA...)
        If Not String.IsNullOrEmpty(voiesDebit) Then
            SendCommand(String.Format("FUNC 'VOLT:DC',(@{0})", voiesDebit))
            SendCommand(String.Format("VOLT:APER 0.05,(@{0})", voiesDebit))
            SendCommand(String.Format("VOLT:RANG:AUTO ON,(@{0})", voiesDebit))
            SendCommand(String.Format("VOLT:DIG 6,(@{0})", voiesDebit))
        End If

        ' Construire la liste de scan
        Dim liste As String
        If Not String.IsNullOrEmpty(voiesTemp) AndAlso Not String.IsNullOrEmpty(voiesDebit) Then
            liste = voiesTemp & "," & voiesDebit
        Else
            liste = If(String.IsNullOrEmpty(voiesTemp), voiesDebit, voiesTemp)
        End If

        ' Compter le nombre de voies
        Dim nbVoies = liste.Split(","c).Length

        ' Séquence de déclenchement identique à l'ancien code
        SendCommand("INIT:CONT OFF")                                    ' Désactiver initiation continue
        SendCommand("TRIG:COUN 1")                                      ' 1 scan par déclenchement
        SendCommand(String.Format("SAMP:COUN {0}", nbVoies))            ' Nombre de voies
        SendCommand(String.Format("ROUT:SCAN (@{0})", liste))           ' Liste de scan
        SendCommand("ROUT:SCAN:TSO IMM")                                ' Démarrage immédiat
        SendCommand("ROUT:SCAN:LSEL INT")                               ' Activer le scan interne
    End Sub

    ''' <summary>
    ''' Configure et démarre le scan sur un Keithley DAQ6510 (carte 7700).
    ''' Utilise ROUTe:SCAN:CREate + INIT au lieu de INIT:CONT ON.
    ''' </summary>
    Public Sub ConfigurerScanDAQ6510(voiesTemp As String, voiesDebit As String,
                                      typeTC As String, intervalleSec As Double)
        SendCommand("*RST")
        Thread.Sleep(500)

        ' Construire la liste triee de tous les numeros de voies
        Dim tousNumeros As New List(Of Integer)
        For Each tok In (voiesTemp & "," & voiesDebit).Split(","c)
            Dim n As Integer
            If Integer.TryParse(tok.Trim(), n) AndAlso n > 0 Then tousNumeros.Add(n)
        Next
        tousNumeros = tousNumeros.Distinct().OrderBy(Function(x) x).ToList()
        If tousNumeros.Count = 0 Then Return

        Dim plage = String.Format("{0}:{1}", tousNumeros.First(), tousNumeros.Last())

        ' Configurer les voies TC sur la plage
        If Not String.IsNullOrEmpty(voiesTemp) Then
            SendCommand(String.Format(":SENSe:FUNCtion 'TEMPerature',(@{0})", plage))
            SendCommand(String.Format(":SENSe:TEMPerature:TRANsducer TCouple,(@{0})", plage))
            SendCommand(String.Format(":SENSe:TEMPerature:TCouple:TYPE {0},(@{1})", typeTC, plage))
            SendCommand(String.Format(":SENSe:TEMPerature:TCouple:RJUNction:RSELect INTernal,(@{0})", plage))
            SendCommand(String.Format(":SENSe:TEMPerature:ODETector ON,(@{0})", plage))
        End If

        ' Configurer les voies tension
        If Not String.IsNullOrEmpty(voiesDebit) Then
            For Each tok In voiesDebit.Split(","c)
                Dim n As Integer
                If Integer.TryParse(tok.Trim(), n) Then
                    SendCommand(String.Format(":SENSe:FUNCtion 'VOLTage:DC',(@{0})", n))
                    SendCommand(String.Format(":SENSe:VOLTage:DC:RANGe:AUTO ON,(@{0})", n))
                End If
            Next
        End If

        ' Memoriser la liste triee pour LireScanDAQ6510
        _voiesDAQ = tousNumeros
    End Sub

    ' Liste des numeros de voies configurees (ordre du scan)
    Private _voiesDAQ As New List(Of Integer)()

    ''' <summary>
    ''' Lit toutes les voies du DAQ6510 via ROUTe:CLOSe + READ? (une par une).
    ''' Seule methode compatible firmware 1.0.04b.
    ''' Retourne les valeurs separees par virgules dans l'ordre de _voiesDAQ.
    ''' </summary>
    Public Function LireScanDAQ6510() As String
        If _voiesDAQ.Count = 0 Then Return ""
        Dim valeurs As New List(Of String)
        For Each voie In _voiesDAQ
            SendCommand(String.Format(":ROUTe:CLOSe (@{0})", voie))
            Thread.Sleep(50)   ' laisser le relais se fermer
            Dim rep = Query(":READ?")
            Dim t = rep.Trim().Split(","c)(0).Trim()
            Dim d As Double
            If t <> "" AndAlso Double.TryParse(t,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, d) Then
                valeurs.Add(t)
            Else
                valeurs.Add("9.9E+37")   ' voie en erreur
            End If
        Next
        Return String.Join(",", valeurs)
    End Function

    Public Sub ConfigurerRelais(voies As List(Of Integer))
        SendCommand("ROUT:OPEN:ALL")
    End Sub

    Public Function LireScan() As String
        ' Calqué sur l'ancien : send("Read?") puis KI2701Read()
        Return Query("Read?")
    End Function

    Public Sub FermerRelais(voie As Integer)
        ' Sortie booléenne ON : tension fixe 5V (compatible ancien code)
        SendCommand(String.Format("OUTP:VOLT 5.0, (@{0})", voie))
    End Sub

    Public Sub OuvrirRelais(voie As Integer)
        ' Sortie booléenne OFF : tension 0V
        SendCommand(String.Format("OUTP:VOLT 0.0, (@{0})", voie))
    End Sub

    ''' <summary>
    ''' Pilote une sortie analogique à une tension précise.
    ''' Utilisé en mode Analogique (0–UMax V).
    ''' </summary>
    Public Sub SetTension(voie As Integer, tensionV As Double)
        SendCommand(String.Format("OUTP:VOLT {0}, (@{1})",
            tensionV.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            voie))
    End Sub

    Public Sub Disconnect()
        Try
            If _stream IsNot Nothing Then _stream.Close()
            If _client IsNot Nothing Then _client.Close()
        Catch
        End Try
        IsConnected = False
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Disconnect()
    End Sub
End Class