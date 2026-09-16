{**********************************************************************
    $Id: imaptools.pas
    This file is part of imapcopy
    Copyright (c) 2001-2009 Armin Diehl

    simple IMAP client object Armin Diehl 2001/07/29

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

    2001/10/05: Changes for Linux
    2002/01/26: Check if internaldate returned by server is correct and
                return nothing if date is invalid
                RFC822MessageDate
    ad 2003/07/15: minor changes for compiling with FreePascal 1.0.10 and 1.1
    ad 2003/12/14: \ was not converted to \\ before sending to server on
                   some places, needed for Notes because the path separator
                   is \
    ad 2004/05/09: Added ReadALineAndCount
    ad 2005/04/10: Fixed memory leak (Objects in Mailboxlist)
    ad 2005/06/16: Added logfile
    ad 2009/07/18: Added timezone conversion
 **********************************************************************}

UNIT imaptools;


{$IFDEF FPC}
  {$MODE Delphi}
{$ENDIF}


INTERFACE

{$IFDEF Unix}
{$LINKLIB c}
{$ENDIF}

USES SysUtils,
     {$IFDEF Win32}
     WinSock,
     {$ELSE}
       {$IFDEF Unix}
       Sockets,
       {$ifdef VER1_0}
         linux,inet,
       {$else}
         unix,baseunix,inet,
       {$endif}
       {$ELSE}
       Error, Unknown Opsys
       {$ENDIF}
     {$ENDIF}
     {enTypes,}Classes, logfile;

{$ifndef delphi}
{$ifndef VER1_0}
{$macro on}
{$define FD_SET:=fpFD_SET}
{$define FD_ZERO:=fpFD_Zero}
{$ENDIF}
{$endif}

CONST
      ReadBufferSizeK = 32768;

TYPE
  {$IFDEF Unix}
  TSocket = LONGINT;
  {$ENDIF}
  IMapServer = CLASS (TObject)
   ServSock              : TSocket;
   State                 : (sDisconnected,sConnected,sLoggedin);
   LastResult            : STRING;
   SeqNo                 : LONGINT;
   Response              : TStringList;
   NumMessages           : INTEGER;
   NumMessagesUnseen     : INTEGER;
   MailboxList           : TStringList;
   {MessageList           : TList;     // UID's of all Messages
}
   CurrentMailbox        : STRING;
   PathSep               : STRING;
   ReadBuf               : ARRAY [1..ReadBufferSizeK] OF CHAR;
   ReadBufPos            : INTEGER;
   ReadBufLen            : INTEGER;
   LastMessageLen        : INTEGER;
   Capabilities          : STRING;
   ServerInfo            : STRING;
   NamespaceUser         : STRING;
   NamespaceOtherUser    : STRING;
   NamespacePublicFolders: STRING;
   DebugCommands         : BOOLEAN;
   ImapUser,ImapPwd,
   IMapServer            : STRING;
   IMapPort              : WORD;
   LogoutInProgess       : BOOLEAN;



   CONSTRUCTOR Create;
   DESTRUCTOR  Destroy; OVERRIDE;

   PROCEDURE FlushInBuffer;
   FUNCTION ReadAChar (VAR C : CHAR; Timeout : INTEGER) : INTEGER;
   FUNCTION ReadALineAndCount (VAR Line      : STRING;
                                   Timeout   : INTEGER;
                               VAR ByteCount : CARDINAL;
                                   MaxLen    : CARDINAL) : INTEGER;
   FUNCTION ReadALine (VAR Line    : STRING;
                           Timeout : INTEGER) : INTEGER;



   FUNCTION Command (Cmd  : STRING; AcceptLen : BOOLEAN) : INTEGER;

   FUNCTION Connect (Host : STRING;
                     Port : WORD)  : INTEGER;
   FUNCTION Login   (User,Password : STRING) : INTEGER;
   FUNCTION Logout                           : INTEGER;
   FUNCTION GetCapabilities                         : STRING;
   FUNCTION GetNamespaces                           : STRING;
   FUNCTION SelectMailbox   (Mailbox : STRING)      : INTEGER;
   FUNCTION CreateMailbox   (Mailbox : STRING)      : INTEGER;
   FUNCTION SubscribeMailbox(Mailbox : STRING)      : INTEGER;
   PROCEDURE ClearMailboxList;
   FUNCTION GetMailboxList                          : INTEGER;  // Result in MailboxList

   //FUNCTION FetchUIDS : INTEGER;  // Gets all UIDS of current selected mailbox (in MessageList)
   //FUNCTION UID (MessageNumber : INTEGER) : CARDINAL;
   FUNCTION MessageSubject (MessageNumber : INTEGER) : STRING;
   FUNCTION MessageFrom    (MessageNumber : INTEGER) : STRING;
   FUNCTION MessageBody    (MessageNumber : INTEGER) : STRING;
   FUNCTION MessageFlags   (MessageNumber : INTEGER) : STRING;
   FUNCTION MessageDate    (MessageNumber : INTEGER) : STRING;
   FUNCTION RFC822MessageDate (MessageNumber : INTEGER) : STRING;  // special for Lotus Notes

   FUNCTION MessageSave    (Mailbox, Msg, Flags, Internaldate : STRING) : INTEGER;

   FUNCTION RemovePersonalNamespace (FolderName : STRING) : STRING;

   FUNCTION isOtherUsersFoler       (FolderName : STRING) : BOOLEAN;
   FUNCTION isPublicFolder          (FolderName : STRING) : BOOLEAN;


END;

{
      \Noinferiors   It is not possible for any child levels of
                     hierarchy to exist under this name; no child levels
                     exist now and none can be created in the future.

      \Noselect      It is not possible to use this name as a selectable
                     mailbox.

      \Marked        The mailbox has been marked "interesting" by the
                     server; the mailbox probably contains messages that
                     have been added since the last time the mailbox was
                     selected.

      \Unmarked      The mailbox does not contain any additional
                     messages since the last time the mailbox was
                     selected.

}

TMailbox = CLASS (TObject)    // Object with additional info for StringList "MailboxList"
  Flags         : STRING;
  Noinferiors   : BOOLEAN;
  Noselect      : BOOLEAN;
  Marked        : BOOLEAN;
  Unmarked      : BOOLEAN;
  HasChildren   : BOOLEAN;

  CONSTRUCTOR Create (Flgs : STRING);
END;

CONST ImapOK                   =  0;
      ImapErr                  = -1;
      ImapErrInvalidStateK     = $FFFF;
      CRLF                     : STRING [2] = #13#10;

PROCEDURE DelGarbage (VAR S : STRING);
FUNCTION NextWord (VAR S : STRING) : STRING;

FUNCTION TCPConnectTo (VAR aSocket      : TSocket;
                           AdressOrName : PCHAR;
                           Port         : WORD) : INTEGER;

PROCEDURE AddTimeZoneConversionEntry (ZoneAsText,ZoneAsImapOffset : STRING);  // e.g. 'UTC','+0000'

IMPLEMENTATION

CONST ImapPortK   = 143;
      StdTimeoutK =  30;  // Seconds per character


{$IFDEF Win32}
VAR
  Initialized: Boolean = False;
  InitData   : TWSAData;
{$ENDIF}
VAR TimeZoneTable : TStringList;

{$IFDEF Unix}
FUNCTION WSAGetLastError : LONGINT;
BEGIN
  Result := errno;
END;

PROCEDURE closesocket (Socket : TSocket);
BEGIN
  {$ifdef VER1_0}
  fdClose (Socket);
  {$else}
  fpclose (Socket);
  {$endif}
END;
{$ENDIF}

FUNCTION TCPConnectTo (VAR aSocket      : TSocket;
                           AdressOrName : PCHAR;
                           Port         : WORD) : INTEGER;
{$IFDEF Unix}
VAR Addr : TInetSockAddr;
    Host : THost;
BEGIN
  Host.NameLookup (AdressOrName);
  IF Host.LastError <> 0 THEN
  BEGIN
    {WriteLn ('Host lookup (',AdressOrName,') failed');}
    Result := 1;
    EXIT;
  END;


  Addr.family := AF_INET;
  Addr.port   := ShortHostToNet (Port);
  {WriteLn ('IP: ',hostaddrtostr (Host.IPAddress));}
  Addr.addr   := HostToNet (LONGINT (Host.IPAddress));
  aSocket := Socket(AF_INET,SOCK_STREAM,0);
  IF NOT Sockets.Connect (aSocket,ADDR,SIZEOF(ADDR)) THEN
  BEGIN
    Result := errno;
    IF errno = 0 THEN Result:= 1;
  END ELSE
  BEGIN
    Result := 0;
    {WriteLn ('Connected errno:',errno,' Socket: ',aSocket);}
  END;
END;
{$ELSE}
VAR Remote_Addr : TSockAddrIn;
    Remote_Host : Phostent;
    A           : TInAddr;
    OptVal      : INTEGER;
BEGIN
  FillChar (Remote_Addr, SIZEOF(Remote_Addr), 0);
  FillChar (Remote_Host, SIZEOF (Remote_Host), 0);
  Remote_Addr.sin_addr.S_addr := 0;
 // try to convert to address
  u_long (A) := inet_addr (AdressOrName);
  IF u_long (A) = u_long (INADDR_NONE) THEN
  BEGIN
    // may be a hostname
    Remote_Host := gethostbyname(AdressOrName);
    IF Remote_Host <> NIL THEN
    BEGIN
      Remote_Addr.sin_addr := PInAddr(Remote_Host^.h_addr_list^)^;
    END;
  END ELSE
    Remote_Addr.sin_addr.S_addr := u_long (A);

  Remote_Addr.sin_family := PF_INET;
  Remote_Addr.sin_port := htons (Port);

  // Ok, Host known, get socket
  IF Remote_Addr.sin_addr.S_addr = 0 THEN
  BEGIN
    Result := WSAGetLastError;
    IF Result = 0 THEN Result := 1;
    aSocket := TSocket (INVALID_SOCKET);
    EXIT;
  END ELSE
    aSocket := socket(PF_INET, SOCK_STREAM, IPPROTO_IP);

  IF aSocket = TSocket (INVALID_SOCKET) THEN
  BEGIN
    Result := WSAGetLastError;
    IF Result = 0 THEN Result := 2;
    EXIT;
  END;

  IF connect (aSocket, Remote_Addr, SIZEOF (Remote_Addr)) <> 0 THEN
  BEGIN
    closesocket (aSocket);
    Result := WSAGetLastError;
    aSocket := TSocket (INVALID_SOCKET);
    IF Result = 0 THEN Result := 3;
    EXIT;
  END;

  OptVal := 1;
  setsockopt (aSocket, IPPROTO_TCP, TCP_NODELAY, PChar (@OptVal), SIZEOF(OptVal));
  Result := 0;
END;
{$ENDIF}


PROCEDURE ImapServer.FlushInBuffer;
VAR Skipped : STRING;
    C       : CHAR;
    P       : INTEGER;
BEGIN
  Skipped := '';
  WHILE ReadBufPos <= ReadBufLen DO
  BEGIN
    Skipped := Skipped + ReadBuf[ReadBufPos];
    INC (ReadBufPos);
  END;
  WHILE (ReadAChar (C, -1) = 0) DO Skipped := Skipped + C;
  IF Skipped <> '' THEN
  BEGIN
    P := Pos (#13,Skipped);
    WHILE (P > 0) DO
    BEGIN
      Delete (Skipped,P,1);
      Insert ('\r',Skipped,P);
      P := Pos (#13,Skipped);
    END;
    P := Pos (#10,Skipped);
    WHILE (P > 0) DO
    BEGIN
      Delete (Skipped,P,1);
      Insert ('\n',Skipped,P);
      P := Pos (#10,Skipped);
    END;
    WriteLn ('Skipped: "',Skipped,'"');
  END;
END;

FUNCTION ImapServer.ReadAChar (VAR C : CHAR; Timeout : INTEGER) : INTEGER;
VAR readfds: TFDSet;
    FA: TTimeVal;
    Err: Integer;
    {$ifdef debug}
    {$ifdef WIN32}
    Len : integer;
    {$endif}
    {$endif}
BEGIN
  IF ReadBufPos <= ReadBufLen THEN
  BEGIN
    C := ReadBuf[ReadBufPos];
    INC (ReadBufPos);
    Result := 0;
    EXIT;
  END;
  ReadBufPos := 1;
  ReadBufLen := 0;

  FillChar (FA, SizeOf (FA), 0);
  IF Timeout = -1 THEN
  BEGIN
    {$IFDEF Unix}
    {$IFDEF VER1_0}
    FA.sec := 0;
    FA.usec:= 0;
    {$ELSE}
    FA.tv_sec := 0;
    FA.tv_usec:= 0;
    {$ENDIF}
    {$ELSE}
    FA.tv_sec := 0;
    FA.tv_usec:= 0;
    {$ENDIF}
  END ELSE
  BEGIN
    {$IFDEF Unix}
    {$ifdef VER1_0}
    FA.sec := Timeout;
    IF Timeout = 0 THEN
      FA.usec:= 1000
    ELSE
      FA.usec:= 0;
    {$else}
    FA.tv_sec := Timeout;
    IF Timeout = 0 THEN
      FA.tv_usec:= 1000
    ELSE
      FA.tv_usec:= 0;
    {$endif}
    {$ELSE}
    FA.tv_sec := Timeout;
    IF Timeout = 0 THEN
      FA.tv_usec:= 1000
    ELSE
      FA.tv_usec:= 0;
    {$ENDIF}
  END;
  {$IFDEF Unix}
  FD_Zero (readfds);
  FD_Set (ServSock, readfds);
  {$ELSE}
  readfds.fd_count:= 1;
  readfds.fd_array[0] := ServSock;
  {$ENDIF}

  {$IFDEF Win32}
  Err := Select (0,          // nfds ignored on Win32
  {$ELSE}
  Err :=  {$ifdef VER1_0}linux.Select{$else}baseunix.FPSelect{$endif} (ServSock+1,
  {$ENDIF}
                 @readfds,
                 NIL,        // @readfds,  // writefds
                 NIL,        // @readfds,  // exceptfds
                 @FA);
  IF Err > 0 THEN
  BEGIN
    ReadBufLen := Recv (ServSock, ReadBuf, SIZEOF(ReadBuf), 0);
    IF ReadBufLen > 0 THEN
      Result := ReadAChar (C, 0)
    ELSE
    BEGIN
      {$IFDEF DEBUG}
      {$ifdef WIN32}
      IF Len = SOCKET_ERROR THEN
        DbgProc ('ReadAChar: Recv() returned SOCKET_ERROR (%d)', [WSAGetLastError])
      ELSE
        DbgProc ('ReadAChar: Recv() returned: %d', [Len]);
      {$ENDIF}
      {$ENDIF}
      Result := WSAGetLastError;
      IF Result = 0 THEN INC (Result);
    END;
  END ELSE
  BEGIN
    {$IFDEF DEBUG}
    {$IFDEF WIN32}
    IF Err = SOCKET_ERROR THEN
      DbgProc ('ReadAChar: Select() returned SOCKET_ERROR (%d)', [WSAGetLastError])
    ELSE
      DbgProc ('ReadAChar: Select() returned: %d', [Err]);
    {$ENDIF}
    {$ENDIF}
    Result := WSAGetLastError;
      IF Result = 0 THEN INC (Result);
  END;
END;

FUNCTION ImapServer.ReadALineAndCount (VAR Line      : STRING;
                                           Timeout   : INTEGER;
                                       VAR ByteCount : CARDINAL;
                                           MaxLen    : CARDINAL) : INTEGER;
VAR C : CHAR;
    S : STRING;
    P : INTEGER;
BEGIN
  C := #0;
  Result := ReadAChar (C, Timeout);
  S := '';
  IF Result <> 0 THEN EXIT;
  Line := '';
  WHILE (Result = 0) AND (C <> #10) DO
  BEGIN
    IF (C <> #13) THEN
    BEGIN
      IF C = #0 THEN    // Ignore #0 in Messages, i.e. the ipswitch imap dont like #0
        C := ' ';
      Line := Line + C;
    END;
    S := S + C;
    inc (ByteCount);
    IF MaxLen > 0 then
      IF ByteCount >= MaxLen then break;
    Result := ReadAChar (C, Timeout);
  END;
  IF Result = 0 THEN
  begin
    S := S + C;
    inc (ByteCount);
  end;
  IF DebugCommands THEN
  BEGIN
    P := Pos (#13,S);
    WHILE (P > 0) DO
    BEGIN
      Delete (S,P,1);
      Insert ('\r',S,P);
      P := Pos (#13,S);
    END;
    P := Pos (#10,S);
    WHILE (P > 0) DO
    BEGIN
      Delete (S,P,1);
      Insert ('\n',S,P);
      P := Pos (#10,S);
    END;
    WriteLn (#13'R:',S);
  END;
END;


FUNCTION ImapServer.ReadALine (VAR Line    : STRING;
                                   Timeout : INTEGER) : INTEGER;
VAR Dummy : CARDINAL;
BEGIN
  Dummy := 0;
  Result := ReadALineAndCount (Line, Timeout, Dummy, 0);
END;


{ rfc2060:
  All interactions transmitted by client and server are in the form of
  lines; that is, strings that end with a CRLF.  The protocol receiver
  of an IMAP4rev1 client or server is either reading a line, or is
  reading a sequence of octets with a known count followed by a line. }

FUNCTION WriteALine (Socket : TSocket; Txt : STRING; DebugCommands: BOOLEAN) : INTEGER;
VAR P : INTEGER;
    S : STRING;
BEGIN
  Result := {$IFDEF Win32}WinSock.{$ENDIF}send (Socket, PChar(Txt)^, Length (Txt), 0);
  IF Result = Length (Txt) THEN
    Result := 0;
  IF DebugCommands THEN
  BEGIN
    S := Txt;
    P := Pos (#13,S);
    WHILE (P > 0) DO
    BEGIN
      Delete (S,P,1);
      Insert ('\r',S,P);
      P := Pos (#13,S);
    END;
    P := Pos (#10,S);
    WHILE (P > 0) DO
    BEGIN
      Delete (S,P,1);
      Insert ('\n',S,P);
      P := Pos (#10,S);
    END;
    WriteLn (#13,'S:',S);
  END;
  IF Result <> 0 THEN
  BEGIN
    Result := WSAGetLastError;
    IF DebugCommands THEN WriteLn ('Error WSA'+IntToStr(Result)+' sending line to server');
  END;
END;


//-----------------------------------------------------------------------------


FUNCTION QuoteSpecialChars (S : STRING) : STRING;
BEGIN
  Result := S;
END;

FUNCTION DecodeCString (S : STRING) : STRING;
VAR P : PCHAR;
BEGIN
  Result := '';
  P := PChar (S);
  WHILE P^ <> #0 DO
  BEGIN
    IF P^ = '\' THEN
    BEGIN
      INC (P);
      CASE P^ OF
        #0  : ;
        'n' : Result := Result + #13;
        'r' : Result := Result + #10;
        't' : Result := Result + #9
      ELSE
        Result := Result + P^;
      END;
      IF P^ <> #0 THEN INC (P);
    END ELSE
    BEGIN
      Result := Result + P^;
      INC (P);
    END;
  END;
END;


FUNCTION CodeCStr (S : STRING) : STRING;
VAR I : INTEGER;
BEGIN
  Result := '';
  FOR I := 1 TO Length (S) DO
  BEGIN
    Result := Result + S[i];
    IF S[i] = '\' THEN
      Result := Result + '\';
  END;
END;


FUNCTION SetQuotationMarksIfNeeded (S : STRING) : STRING;
BEGIN
  IF Pos (' ', S) > 0 THEN
    Result := '"' + CodeCStr (S) + '"'
  ELSE
  BEGIN
    Result := CodeCStr (S);
    // Notes wants to have Strings containing \\ in Quotaion Marks !!
    IF (Pos ('\\',Result) <> 0) THEN
      Result := '"' + Result + '"';
  END;
END;


PROCEDURE GetListResults (Line : STRING;
                          VAR Flags, PathSeparator, Name : STRING);
BEGIN
  // * LIST (\HasNoChildren) "." "INBOX.Archiv-Diverses"
  // * LIST () "/" foo/bar
  Flags := '';
  PathSeparator := '';
  Name := '';
  IF NextWord (Line) <> '*' THEN EXIT;
  IF UpperCase (NextWord (Line)) <> 'LIST' THEN EXIT;
  Flags := NextWord (Line);
  PathSeparator := NextWord (Line);
  PathSeparator := DecodeCString (PathSeparator);  // i.e. for notes imap -> "\\"
  Name := NextWord (Line);
END;




CONSTRUCTOR ImapServer.Create;
BEGIN
  INHERITED Create;
  Response := TStringList.Create;
  MailboxList := TStringList.Create;
  //MessageList := TList.Create;
  NumMessages := 0;
  SeqNo := 1;
  State := sDisconnected;
  CurrentMailbox := '';
  ReadBufLen        := 0;
  ReadBufPos        := 1;
  NamespaceUser     := '';
  NamespaceOtherUser := '';
  NamespacePublicFolders:= '';
  ServerInfo        := '';
  DebugCommands     := FALSE;
  LogoutInProgess   := FALSE;
END;


DESTRUCTOR  ImapServer.Destroy;
BEGIN
  IF State > sDisconnected THEN
    Logout;
  IF State >= sConnected THEN
    closesocket (ServSock);
  Response.Free;
  ClearMailboxList;
  MailboxList.Free;
  //MessageList.Free;
END;

FUNCTION ImapServer.Command (Cmd  : STRING; AcceptLen : BOOLEAN) : INTEGER;
VAR Line    : STRING;
    Seq     : STRING;
    First   : BOOLEAN;
    P       : PCHAR;
    LenStr  : STRING;
    Received    : CARDINAL;
    IgnoreRest  : BOOLEAN;
BEGIN
  Response.Clear;
  LastMessageLen := 0;
  Seq := 'A' + IntToHex (SeqNo,4) + ' ';
  INC (SeqNo); IF SeqNo > $FFFF THEN SeqNo := 1;
  Line := Seq + Cmd + CRLF;

  Result := WriteALine (ServSock, Line, DebugCommands);
  IF Result <> 0 THEN
  BEGIN
    IF NOT LogoutInProgess THEN
    BEGIN
      log (log_error,'Error '+IntToStr(Result)+' sending line to server, connection lost ?');
      IF IMapServer <> '' THEN
      BEGIN
        Write ('Trying to reconnect to ',IMapServer,' ');
        closesocket (ServSock);
        IF Connect (IMapServer, IMapPort) = 0 THEN
        BEGIN
          IF Login (IMapUser,IMapPwd) = 0 THEN
          BEGIN
            log (log_message,'Reconnect/Login OK');
            IF CurrentMailbox <> '' THEN
              IF SelectMailbox (CurrentMailbox) <> 0 THEN
              BEGIN
                log (log_error, 'Select Mailbox "'+CurrentMailbox+'" after reconnect failed');
                EXIT;
              END;
            FlushInBuffer;
            Result := WriteALine (ServSock, Line, DebugCommands);
            IF Result <> 0 THEN
            BEGIN
              log (log_error, 'Error '+IntToStr(Result)+' sending line to server, connection lost ?');
              EXIT;
            END;
          END ELSE
          BEGIN
            log (log_error, 'LOGIN as "'+IMapUser+'" FAILED');
            EXIT;
          END;
        END ELSE
        BEGIN
          Write (#13);
          log (log_error,'Reconnect to Server '+IMapServer+' failed');
          IMapServer := '';
        END;
      END;
    END;
    EXIT;
  END;

  // Read Results until a line begins with Seq
  LastResult := '';
  First := TRUE; IgnoreRest := FALSE;
  REPEAT
    Result := ReadALine (Line, StdTimeoutK);
    IF First THEN
    BEGIN
      IF AcceptLen THEN
      BEGIN
        IF (Result = 0) AND (Copy (Line,1,1) = '*') AND (Line[Length(Line)] = '}') THEN
        BEGIN   // Server sends length of data
          LenStr := '';
          P := @Line[Length(Line)-1];
          WHILE P^ <> '{' DO
          BEGIN
            LenStr := P^ + LenStr;
            DEC (P);
          END;
          LastMessageLen := StrToInt (LenStr);
          //WriteLn ('Got Len: ',LastMessageLen);
          Received := 0;
          //Response.Add (Line);
          Result := ReadALineAndCount (Line, StdTimeoutK, Received, LastMessageLen);
          WHILE (Result = 0) AND (Received < LastMessageLen) DO
          BEGIN
            Response.Add (Line);
            //INC (Received, Length (Line) + Length (CRLF));
            IF (Received < LastMessageLen) THEN
              Result := ReadALineAndCount (Line, StdTimeoutK, Received, LastMessageLen)
            ELSE
            BEGIN
              Line := '';
              IgnoreRest := TRUE;
            END;
          END;
          Response.Add (Line); //Kasper 2007-03-14: Store last line
          IgnoreRest := true;
        END;
      END;
    END;
    IF (Result = 0) AND (Copy (Line,1,1) = '+') THEN
    BEGIN
      IF DebugCommands THEN
        WriteLn ('C: Command exit because of + ("'+Line+'")');
      EXIT;  // i.e. for APPEND
    END;
    First := FALSE;

    IF Result = 0 THEN
      IF NOT IgnoreRest THEN Response.Add (Line);
  UNTIL (Result <> 0) OR (Copy (Line,1,Length(Seq)) = Seq);
  IF Result = 0 THEN
  BEGIN
    LastResult := Copy (Line,Length(Seq)+1,Length(Line));
    WHILE (Length (LastResult) > 0) AND (Copy (LastResult,1,1) <= ' ') DO
      Delete (LastResult,1,1);
    IF UpperCase (Copy(LastResult,1,2)) <> 'OK' THEN
      Result := ImapErr;
  END ELSE
  BEGIN
    IF DebugCommands THEN
      WriteLn ('C: Command status '+IntToStr(Result));
  END;
END;


FUNCTION ImapServer.Connect (Host : STRING;
                             Port : WORD)  : INTEGER;
BEGIN
  IF Port = 0 THEN Port := ImapPortK;
  Result := TCPConnectTo (ServSock, PChar (Host), Port);
  IF Result = 0 THEN
  BEGIN
    State := sConnected;
    Result := ReadALine (ServerInfo, StdTimeoutK);
    NextWord (ServerInfo);  // *
    NextWord (ServerInfo);  // OK
    IMapServer := Host;
    IMapPort   := Port;
    ReadBufPos := 1;
    ReadBufLen := 0;
  END;
END;


FUNCTION DecodeNamespaces ( NS : STRING;
                             VAR NSUser, NSotherUser, NSPublic,
                                SepUser, SepOtherUser, SepPublic : STRING) : BOOLEAN;
VAR S : STRING;
BEGIN
  NSUser := '';
  NSotherUser := '';
  NSPublic := '';
  SepUser := '';
  SepOtherUser := '';
  SepPublic := '';
  NextWord (NS);
  S := UpperCase (NextWord (NS));
  Result := FALSE;
  IF S <> 'NAMESPACE' THEN EXIT;
  S := NextWord (NS);
  IF UpperCase (S) <> 'NIL' THEN
  BEGIN
    S := NextWord (S);   // remove ()
    NSUser := NextWord (S);
    SepUser := DecodeCString (NextWord (S));  // AD 2003/11/04 for Notes
    IF (NSUser <> '') AND (SepUser <> '') THEN
      IF Copy (NSUser,Length(NSUser)-Length(SepUser)+1,Length(SepUser)) = SepUser THEN
        NSUser := Copy (NSUser,1,Length(NSUser)-Length(SepUser));
  END;
  S := NextWord (NS);
  IF UpperCase (S) <> 'NIL' THEN
  BEGIN
    S := NextWord (S);   // remove ()
    NSOtherUser := NextWord (S);
    SepOtherUser := DecodeCString (NextWord (S));  // AD 2003/11/04 for Notes
    IF (NSOtherUser <> '') AND (SepOtherUser <> '') THEN
      IF Copy (NSOtherUser,Length(NSOtherUser)-Length(SepOtherUser)+1,Length(SepOtherUser)) = SepOtherUser THEN
        NSOtherUser := Copy (NSOtherUser,1,Length(NSOtherUser)-Length(SepOtherUser));
  END;
  S := NextWord (NS);
  IF UpperCase (S) <> 'NIL' THEN
  BEGIN
    S := NextWord (S);   // remove ()
    NSPublic := NextWord (S);
    SepPublic := DecodeCString (NextWord (S)); // AD 2003/11/04 for Notes
    IF (NSPublic <> '') AND (SepPublic <> '') THEN
      IF Copy (NSPublic,Length(NSPublic)-Length(SepPublic)+1,Length(SepPublic)) = SepPublic THEN
        NSPublic := Copy (NSPublic,1,Length(NSPublic)-Length(SepPublic));
  END;
  Result := TRUE;
END;

FUNCTION ImapServer.Login   (User,Password : STRING) : INTEGER;
VAR S : STRING;
    NamespaceOk : BOOLEAN;
BEGIN
  IF State <> sConnected THEN
  BEGIN
    Result := ImapErrInvalidStateK;
    EXIT;
  END;
  Result := Command ('LOGIN '+SetQuotationMarksIfNeeded (QuoteSpecialChars(User))+
    ' '+SetQuotationMarksIfNeeded (QuoteSpecialChars(Password)),TRUE);
  IF Result = 0 THEN
  BEGIN
    IMapUser := User;
    IMapPwd  := Password;
    NamespaceUser := '';

    Capabilities := UpperCase (GetCapabilities);
    NamespaceOk := FALSE;
    IF Pos (' NAMESPACE', Capabilities) > 0 THEN
    BEGIN
      S := GetNamespaces;
      IF DecodeNamespaces (S,NamespaceUser,NamespaceOtherUser,NamespacePublicFolders,PathSep,S,S) THEN
      BEGIN
        IF PathSep <> '' THEN
          NamespaceOk := TRUE
        ELSE
          log (log_error,'Server error: NAMESPACE returned no path sepearator for personal namespace ?');
      END ELSE
        log (log_warning, 'Ops: Server said NAMESPACE is supported but NAMESPACE command failed');
    END;

    IF NOT NamespaceOk THEN
    BEGIN
      // determine path seperator from INBOX
      Result := Command ('LIST "" INBOX',FALSE);
      IF Result = 0 THEN
      BEGIN
        IF Response.Count > 0 THEN
        BEGIN
          GetListResults (Response[0],S,PathSep, S);
          IF DebugCommands THEN
            WriteLn ('Path Seperator >',PathSep,'<');
        END ELSE
        BEGIN
          log (log_error, 'Unable to determine Path seperator');
          Result := -1;
        END;
      END;
    END;
  END;
  FlushInBuffer;
END;

FUNCTION ImapServer.Logout                           : INTEGER;
BEGIN
  TRY
    LogoutInProgess := TRUE;
    Result := Command ('LOGOUT',TRUE);
    IF Result = 0 THEN
      State := sConnected;
    FlushInBuffer;
  FINALLY
    LogoutInProgess := FALSE;
  END;
END;

FUNCTION ImapServer.GetCapabilities : STRING;
BEGIN
  IF Command ('CAPABILITY',TRUE) = 0 THEN
  BEGIN
    Result:= Response[0];
    NextWord (Result); // *
    NextWord (Result); // CAPABILITY
  END ELSE
    Result := '';
END;


FUNCTION ImapServer.GetNamespaces : STRING;
BEGIN
  IF Command ('NAMESPACE',TRUE) = 0 THEN
    Result:= Response[0]
  ELSE
    Result := '';
END;


FUNCTION ImapServer.SelectMailbox  (Mailbox       : STRING) : INTEGER;
VAR S,Keyword,Count : STRING;
    I : INTEGER;
BEGIN
  NumMessages       := 0;
  NumMessagesUnseen := 0;
  CurrentMailbox    := '';
  Result := Command ('SELECT '+SetQuotationMarksIfNeeded (Mailbox),TRUE);
  IF Result = 0 THEN
  BEGIN
    CurrentMailbox := Mailbox;
    IF Response.Count > 0 THEN
      FOR I := 0 TO Response.Count-1 DO
      BEGIN
        S := Response [I];
        IF (NextWord (S) = '*') THEN
        BEGIN
          Count := NextWord (S);
          Keyword := UpperCase (NextWord (S));
          IF Keyword = 'EXISTS' THEN
          BEGIN
            TRY
              NumMessages := StrToInt (Count);
            EXCEPT
              NumMessages := 0;
              log (log_error, 'Error: Server returned non numeric response before EXISTS ("'+Response [I]+'"');
            END;
          END ELSE
          IF Keyword = 'RECENT' THEN
          BEGIN
            TRY
              NumMessagesUnseen := StrToInt (Count);
            EXCEPT
              NumMessagesUnseen := 0;
              log (log_error, 'Error: Server returned non numeric response before RECENT ("'+Response [I]+'"');
            END;
          END;
        END;
      END;
  END;
  FlushInBuffer;
END;


FUNCTION ImapServer.SubscribeMailbox  (Mailbox       : STRING) : INTEGER;
BEGIN
  Result := Command ('SUBSCRIBE '+SetQuotationMarksIfNeeded (Mailbox),TRUE);
  FlushInBuffer;
END;


FUNCTION ImapServer.CreateMailbox  (Mailbox       : STRING) : INTEGER;
BEGIN
  Result := Command ('CREATE '+SetQuotationMarksIfNeeded (Mailbox),TRUE);
  FlushInBuffer;
END;

PROCEDURE DelGarbage (VAR S : STRING);
BEGIN
  WHILE (Length (S) > 0) AND (Copy (S,1,1) <= ' ') DO
    Delete (S,1,1);
END;

PROCEDURE SucheKlZu (CONST S : STRING; VAR P : INTEGER);
BEGIN
  IF S[P] <> '(' THEN EXIT;
  INC (P);
  WHILE (P <= Length (S)) AND (S[P] <> ')') DO
  BEGIN
    IF S[P] = '"' THEN
    BEGIN
      INC (P);
      WHILE (P <= Length(S)) AND (S[P] <> '"') DO
        INC (P);
    END;
    IF S[P] = '(' THEN SucheKlZu (S,P);
    INC (P);
  END;
END;


FUNCTION NextWord (VAR S : STRING) : STRING;
VAR P : INTEGER;
BEGIN
  DelGarbage (S);
  IF Length (S) = 0 THEN
  BEGIN
    Result := S;
    EXIT;
  END;
  IF S[1] = '(' THEN
  BEGIN
    P := 1;
    SucheKlZu (S,P);
    Result := Copy (S,2,P-2);
    Delete (S,1,P);
  END ELSE
  IF S[1] = '"' THEN
  BEGIN
    P := 2;
    WHILE (P <= Length (S)) AND (S[P] <> '"') DO
      INC (P);
    Result := Copy (S,2,P-2);
    Delete (S,1,P);
  END ELSE
  BEGIN
    P := Pos (' ', S);
    IF P = 0 THEN
    BEGIN
      Result := S;
      S := '';
    END ELSE
    BEGIN
      Result := Copy (S,1,P-1);
      Delete (S,1,P);
    END;
  END;
END;


PROCEDURE ImapServer.ClearMailboxList;
VAR I : INTEGER;
BEGIN
  IF Mailboxlist.Count > 0 then
  begin
    for i := 0 to Mailboxlist.Count-1 do
    begin
      TMailbox (Mailboxlist.Objects[i]).Free;
      Mailboxlist.Objects[i] := nil;
    end;
    Mailboxlist.Clear;
  end;
END;


FUNCTION ImapServer.GetMailboxList                          : INTEGER;  // Result in MailboxList
VAR Flags, PathSeparator, Name, S : STRING;
    I,ListPos : INTEGER;
BEGIN
  //MailboxList.Clear;
  ClearMailboxList;
  Flags := '';
  PathSeparator := '';
  Result := Command ('LIST "" *',FALSE);
  IF Result = 0 THEN
    IF Response.Count > 0 THEN
    BEGIN
      // This is a hack for lotus notes, notes sends {len} in list responses also
      I := 0;
      WHILE (I < Response.Count) DO
      BEGIN
        Name := Response[I];
        IF (Copy (Name,1,1) = '*') AND (Name[Length(Name)] = '}') AND (I < Response.Count-1) THEN
        BEGIN
          WHILE (Name <> '') AND (Name[Length(Name)] <> '{') DO
            Name := Copy (Name,1,Length(Name)-1);
          IF Name <> '' THEN
            Name := Copy (Name,1,Length(Name)-1);
          INC (I);
          S := Response[I];
          IF Pos (' ', S) > 0 THEN    // AD 2003/12/14
            S := '"' + S + '"';
          Name := Name + ' ' + S;
          Response[I-1] := Name;
          Response.Delete (I);
        END ELSE
          INC (I);
      END;
      FOR I := 0 TO Response.Count-1 DO
      BEGIN
        GetListResults (Response[I], Flags, PathSeparator, Name);
        IF Copy (Name,1,1) = '"' THEN
          Name := Copy (Name,2,Length(Name)-2);
        IF Name <> '' THEN
        BEGIN
          ListPos := MailboxList.Add (Name);
          Mailboxlist.Objects[ListPos] := TMailbox.Create (Flags);
        END;
      END;
    END;
END;


{--
FUNCTION ImapServer.FetchUIDS : INTEGER;  // Gets all UIDS of current selected mailbox (in MessageList)
VAR S   : STRING;
    UID : CARDINAL;
    I   : INTEGER;
BEGIN
  MessageList.Clear;
  Result := Command ('UID FETCH 1:* (UID)');
  IF Result = 0 THEN
    IF Response.Count > 0 THEN
      FOR I := 0 TO Response.Count-1 DO
      BEGIN
        S := Response[I];
        IF NextWord (S) = '*' THEN
        BEGIN
          NextWord (S);  // Message Number
          IF UpperCase (NextWord (S)) = 'FETCH' THEN
          BEGIN
            S := NextWord (S);    // UID xxxx
            DelGarbage (S);
            IF UpperCase (Copy (S,1,3)) = 'UID' THEN
            BEGIN
              Delete (S,1,3); DelGarbage (S);
              UID := StrToInt (S);  // FIXME: StrToInt returns integer, we need cardinal
              MessageList.Add (POINTER (UID));
            END;
          END;
        END;
      END;
  NumMessages := MessageList.Count;
END;


FUNCTION ImapServer.UID (MessageNumber : INTEGER) : CARDINAL;
BEGIN
  IF (MessageNumber < 1) OR (MessageNumber > NumMessages) THEN
    Result := 0
  ELSE
    Result := CARDINAL (MessageList[MessageNumber-1]);
END;
--}

FUNCTION ImapServer.MessageSubject (MessageNumber : INTEGER) : STRING;
BEGIN
  Result := '';
  IF MessageNumber > 0 THEN
  BEGIN
    IF Command ('FETCH '+IntToStr(MessageNumber)+' (BODY[HEADER.FIELDS (Subject)])',TRUE) = 0 THEN
    BEGIN
      IF Response.Count > 0 THEN
        Result := Copy (Response[0],10,512);
    END;
  END;
END;


FUNCTION ImapServer.MessageFrom (MessageNumber : INTEGER) : STRING;
BEGIN
  Result := '';
  IF MessageNumber > 0 THEN
  BEGIN
    IF Command ('FETCH '+IntToStr(MessageNumber)+' (BODY[HEADER.FIELDS (From)])',TRUE) = 0 THEN
    BEGIN
      IF Response.Count > 0 THEN
        Result := Copy (Response[0],7,512);
    END;
  END;
END;


FUNCTION ImapServer.MessageBody  (MessageNumber : INTEGER) : STRING;
VAR TotalLen,L,U : CARDINAL;
    P : PCHAR;
BEGIN
  IF Response.Capacity < 655535 THEN
    Response.Capacity := 65535;
  Result := '';
  IF MessageNumber > 0 THEN
  BEGIN
    // IF Command ('FETCH '+IntToStr(MessageNumber)+' (BODY[])',TRUE) = 0 THEN
    IF Command ('FETCH '+IntToStr(MessageNumber)+' (BODY.PEEK[])',TRUE) = 0 THEN
    BEGIN
      IF Response.Count > 0 THEN
      BEGIN
        TotalLen := 0;
        FOR U := 0 TO Response.Count-1 DO
        BEGIN
          INC (TotalLen, Length (Response[U]));
          INC (TotalLen,2);  // CRLF
        END;
        SetLength (Result, TotalLen);
        {Result := '';
         FOR U := 0 TO Response.Count-1 DO
          Result := Result + Response [U] + CRLF;}       // This is horrible slow on large messages using delphi3 !!
        P := PChar (Result);
        FOR U := 0 TO Response.Count-1 DO
        BEGIN
          L := Length (Response[U]);
          Move (PChar(Response[U])^, P^, L);
          INC (P, L);
          Move (CRLF[1], P^, 2);
          INC (P, 2);
        END;
      END;
    END;
    FlushInBuffer;
  END;
END;


FUNCTION ImapServer.MessageFlags (MessageNumber : INTEGER) : STRING;
VAR S : STRING;
    U : INTEGER;
BEGIN
  Result := '';
  IF MessageNumber > 0 THEN
  BEGIN
    IF Command ('FETCH '+IntToStr(MessageNumber)+' (FLAGS)',TRUE) = 0 THEN
    BEGIN
      IF Response.Count > 0 THEN
        FOR U := 0 TO Response.Count-1 DO
        BEGIN
          S := Response [U];
          IF NextWord (S) = '*' THEN
          BEGIN
            NextWord (S);
            IF UpperCase (NextWord (S)) = 'FETCH' THEN
            BEGIN
              S := NextWord (S);   // FLAGS (\bla \bla)
              IF UpperCase (NextWord(S)) = 'FLAGS' THEN
              BEGIN
                Result := NextWord (S);
                EXIT;
              END;
            END;
          END;
        END;
    END;
    FlushInBuffer;
  END;
END;


{ i.e. 05-Jul-2001 10:03:50 +0200 }

FUNCTION MessageDateValid (VAR OrgMsgDateTime : STRING) : BOOLEAN;
VAR I, J        : INTEGER;
    ValidMonth  : STRING;
    MsgDateTime : STRING;
    st          : STRING;
BEGIN
  Result := FALSE;
  //OrgMsgDateTime := copy(OrgMsgDateTime,1,20) + ' UT';  // TEST
  MsgDateTime := OrgMsgDateTime;
  {day}
  I := Pos ('-', MsgDateTime); IF I < 2 THEN EXIT;
  IF I = 2 THEN
    OrgMsgDateTime := '0' + OrgMsgDateTime; { ad 2003/11/21: Insert a 0 if day in date is one digit }

  J := 0;
  TRY
    J := StrToInt (Copy (MsgDateTime,1,I-1));
  EXCEPT
  END;
  IF (J < 1) OR (J > 31) THEN EXIT;
  Delete (MsgDateTime,1,I);

  {month}
  ValidMonth := 'JAN,FEB,MAR,APR,MAY,JUN,JUL,AUG,SEP,OCT,NOV,DEC,';
  I := Pos ('-', MsgDateTime); IF I < 4 THEN EXIT;
  J := Pos (AnsiUpperCase (Copy (MsgDateTime,1,I-1))+',', ValidMonth);
  IF J = 0 THEN EXIT;
  Delete (MsgDateTime,1,I);

  {year}
  I := Pos (' ', MsgDateTime); IF I < 3 THEN EXIT;
  J := 0;
  TRY
    J := StrToInt (Copy (MsgDateTime,1,I-1));
  EXCEPT
  END;
  IF J = 0 THEN EXIT;
  // not sure, is a 2 digit year allowed ?
  IF (J < 100) AND (J > -1) THEN
    IF J > 50 THEN
      INC (J,1900)
    ELSE
      INC (J,2000);
  // We allow 1970 to 2200 as valid years here
  IF (J < 1970) OR (J > 2200) THEN EXIT;
  Delete (MsgDateTime,1,I);

  {hour}
  I := Pos (':', MsgDateTime); IF I < 3 THEN EXIT;
  J := -1;
  TRY
    J := StrToInt (Copy (MsgDateTime,1,I-1));
  EXCEPT
  END;
  IF (J < 0) OR (J > 23) THEN EXIT;
  Delete (MsgDateTime,1,I);

  {Minute}
  I := Pos (':', MsgDateTime); IF I < 3 THEN EXIT;
  J := -1;
  TRY
    J := StrToInt (Copy (MsgDateTime,1,I-1));
  EXCEPT
  END;
  IF (J < 0) OR (J > 59) THEN EXIT;
  Delete (MsgDateTime,1,I);

  {Second}
  I := Pos (' ', MsgDateTime); IF I < 3 THEN EXIT;
  J := -1;
  TRY
    J := StrToInt (Copy (MsgDateTime,1,I-1));
  EXCEPT
  END;
  IF (J < 0) OR (J > 59) THEN EXIT;
  Delete (MsgDateTime,1,I);

  {offset +0000 -0000}
  MsgDateTime := Trim(MsgDateTime);  // in case we have something like "+0000 (GMT)" remove the (...
  I := Pos (' ',MsgDateTime);
  IF (I > 0) then delete (MsgDateTime,I,255);

  // check if we have a conversion entry
  st := TimeZoneTable.Values [MsgDateTime];
  IF st <> '' THEN
  BEGIN
    MsgDateTime := OrgMsgDateTime;
    OrgMsgDateTime := copy(OrgMsgDateTime,1,20)+' '+st;
    //WriteLn ('** converted timezone from "'+MsgDateTime+'" to "'+OrgMsgDateTime+'"');
  END;

  Result := TRUE;
END;


FUNCTION ImapServer.RFC822MessageDate (MessageNumber : INTEGER) : STRING;
VAR U : CARDINAL;
    S,S2 : STRING;
    I, J : INTEGER;
    Resu, Validmonth : STRING;
BEGIN
  Result := '';
  U := MessageNumber; // UID (MessageNumber);
  IF U > 0 THEN
  BEGIN
    IF Command ('FETCH '+IntToStr(U)+' (BODY[HEADER.FIELDS (DATE)])',TRUE) = 0 THEN
    BEGIN
      IF Response.Count > 0 THEN
        FOR U := 0 TO Response.Count-1 DO
        BEGIN
          S := Response [U];
          IF NextWord (S) = 'Date:' THEN
          BEGIN
            // 27 Aug 76 0932 PDT
            // Tue, 21 Mar 2000 15:51:42 +0100
           { from rfc822:
             dates       =   orig-date                   ; Original
                           [ resent-date ]               ; Forwarded
             date-time   =  [ day "," ] date time        ; dd mm yy
                                                         ;  hh:mm:ss zzz
             day         =  "Mon"  / "Tue" /  "Wed"  / "Thu" /  "Fri"  / "Sat" /  "Sun"}

            // needed format: 01-Apr-2001 11:00:55 +0200
            I := Pos (',', S);
            IF I > 0 THEN Delete (S,1,I);  // we dont need the day
            S2 := NextWord (S);            // date: dd
            J := -1;
            TRY J := StrToInt (S2); EXCEPT END;
            IF (J < 1) OR (J > 31) THEN EXIT;
            S2 := IntToStr (J);
            IF Length (S2) < 2 THEN S2 := '0' + S2;
            Resu := S2 + '-';

            ValidMonth := 'jan,feb,mar,apr,may,jun,jul,aug,sep,oct,nov,dec,';
            S2 := AnsiLowerCase (Trim (NextWord (S)));  // date: mm
            IF Pos (S2+',', ValidMonth) = 0 THEN EXIT;
            Resu := Resu + UpCase (S2[1]) + S2[2] + S2[3] + '-';

            S2 := NextWord (S);   // date: yy
            J := -1;
            TRY J := StrToInt (S2); EXCEPT END;
            IF J < 0 THEN EXIT;
            // 2 digit year ?
            IF J < 100 THEN
              IF J < 50 THEN
                INC (J,2000)
              ELSE
                INC (J,1900);
            IF (J < 1970) OR (J > 2200) THEN EXIT;
            Resu := Resu + IntToStr (J) + ' ';

            // Time: hh:mm:ss zzz
            // or    hh:mm:ss +xxxx
            // or    hh:mm:ss -xxxx
            I := Pos (':', S); IF I = 0 THEN EXIT;
            S2 := Copy (S,1,I-1); Delete (S,1,I);
            J := -1;
            TRY J := StrToInt (S2); EXCEPT END;
            IF (J < 0) OR (J > 23) THEN EXIT;
            Resu := Resu + S2 + ':';

            I := Pos (':', S); IF I = 0 THEN EXIT;
            S2 := Copy (S,1,I-1); Delete (S,1,I);
            J := -1;
            TRY J := StrToInt (S2); EXCEPT END;
            IF (J < 0) OR (J > 59) THEN EXIT;
            Resu := Resu + S2 + ':';

            I := Pos (' ', S); IF I = 0 THEN EXIT;
            S2 := Copy (S,1,I-1); Delete (S,1,I);
            J := -1;
            TRY J := StrToInt (S2); EXCEPT END;
            IF (J < 0) OR (J > 59) THEN EXIT;
            Resu := Resu + S2 + ' ';

            S := Trim (NextWord (S));
            // I do not have a list of valid codes and offsets so i can only
            // accept the +-xxxx, set +0000 for unknown values
            IF Length (S) = 5 THEN
              IF (S[1] = '+') OR (S[1] = '-') AND
                  (S[2] IN ['0'..'9']) AND
                  (S[3] IN ['0'..'9']) AND
                  (S[4] IN ['0'..'9']) AND
                  (S[5] IN ['0'..'9']) THEN
              BEGIN
                Result := Resu + S;
                EXIT;
              END;
            Result := Resu + '+0000';
          END;
        END;
    END;
  END;
END;


FUNCTION ImapServer.MessageDate (MessageNumber : INTEGER) : STRING;
VAR U : CARDINAL;
    S : STRING;
BEGIN
  Result := '';
  U := MessageNumber; // UID (MessageNumber);
  IF U > 0 THEN
  BEGIN
    IF Command ('FETCH '+IntToStr(U)+' (INTERNALDATE)',TRUE) = 0 THEN
    BEGIN
      IF Response.Count > 0 THEN
        FOR U := 0 TO Response.Count-1 DO
        BEGIN
          S := Response [U];
          IF NextWord (S) = '*' THEN
          BEGIN
            NextWord (S);
            IF UpperCase (NextWord (S)) = 'FETCH' THEN
            BEGIN
              S := NextWord (S);   // FLAGS (\bla \bla)
              IF UpperCase (NextWord(S)) = 'INTERNALDATE' THEN
              BEGIN
                Result := NextWord (S);
                IF NOT MessageDateValid (Result) THEN
                BEGIN
                  IF DebugCommands THEN
                    log (log_error,' ** INVALID DATE RECEIVED: "'+Result+'"');
                  Result := '';  // AD 26.01.2002
                END;
                EXIT;
              END;
            END;
          END;
        END;
    END;
  END;
END;


FUNCTION ImapServer.MessageSave    (Mailbox, Msg, Flags, Internaldate : STRING) : INTEGER;
VAR S : STRING;
    OldDbg  : BOOLEAN;
BEGIN
  S := '';
  OldDbg := DebugCommands;

  Mailbox := SetQuotationMarksIfNeeded (Mailbox);

  IF (Flags <> '') THEN
    Flags := ' ('+Flags+')';

  IF Internaldate <> '' THEN
    Flags := Flags + ' "' + Internaldate+'"';


  Result := Command ('APPEND '+Mailbox + Flags + ' {' + IntToStr (Length(Msg)) + '}',TRUE);
  IF Result = 0 THEN
  BEGIN
    IF OldDbg THEN
    BEGIN
      WriteLn (#13'S: Message not shown (',Length(Msg),' Bytes)');
      DebugCommands := FALSE;
    END;
    {IF Length(Msg) = 4095 THEN
    BEGIN
      WriteLn;
    END;}
    Result := WriteALine (ServSock, Msg + #13#10, DebugCommands);  // why the hell do i need 1 additional linefeed ?

    DebugCommands := OldDbg;
    IF Result = 0 THEN
    BEGIN
      Result := ReadALine (S, StdTimeoutK);
      IF (Result <> 0) THEN
      BEGIN
        {$IFDEF Unix}
	{$ifdef VER1_0}
	IF (Result = Sys_ECONNRESET) OR (Result = Sys_ECONNABORTED) THEN
	{$else}
	IF (Result = ESysECONNRESET) OR (Result = ESysECONNABORTED) THEN
	{$endif}
	{$ELSE}
        IF (Result = WSAECONNRESET) OR (Result = WSAECONNABORTED) THEN
	{$ENDIF}
          LastResult := '[ Server closed connection after message was sent, Err:'+IntToStr(Result)+' ]'
        ELSE
          LastResult := '[ Error '+IntToStr(Result)+' receiving data from server ]';
      END;
      WHILE (Result = 0) AND (Copy (S,1,1) = '*') DO
        // Result := ReadALine (S, -1);
	Result := ReadALine (S, StdTimeoutK);
      IF Result = 0 THEN
      BEGIN
        NextWord (S);   // Skip Axxx or *
        IF UpperCase (NextWord (S)) <> 'OK' THEN
        BEGIN
          Result := IMapErr;
          LastResult := S;
        END;
      END;
      FlushInBuffer;
    END;
  END;
END;  // ImapServer.MessageSave


FUNCTION ImapServer.RemovePersonalNamespace (FolderName : STRING) : STRING;
VAR S : STRING;
BEGIN
  IF (NamespaceUser <> '') THEN
  BEGIN
    S := NamespaceUser + PathSep;
    IF (Copy (FolderName,1,Length(S)) = S) THEN
      Result := Copy (FolderName,Length(S)+1,Length(FolderName))
    ELSE
      Result := FolderName;
  END ELSE
    Result := FolderName;
END;



FUNCTION ImapServer.isOtherUsersFoler       (FolderName : STRING) : BOOLEAN;
BEGIN
  Result := FALSE;
  IF NamespaceOtherUser = '' THEN EXIT;
  IF FolderName = NamespaceOtherUser THEN
    Result := TRUE
  ELSE
  IF Length (FolderName) >= (Length (NamespaceOtherUser)+Length(PathSep)) THEN
    IF Copy (FolderName,1,Length(NamespaceOtherUser)+Length(PathSep)) = NamespaceOtherUser+PathSep THEN
      Result := TRUE;
END;

FUNCTION ImapServer.isPublicFolder          (FolderName : STRING) : BOOLEAN;
BEGIN
  Result := FALSE;
  IF NamespacePublicFolders = '' THEN EXIT;
  IF FolderName = NamespacePublicFolders THEN
    Result := TRUE
  ELSE
  IF Length (FolderName) >= (Length (NamespacePublicFolders)+Length(PathSep)) THEN
    IF Copy (FolderName,1,Length(NamespacePublicFolders)+Length(PathSep)) = NamespacePublicFolders+PathSep THEN
      Result := TRUE;
END;


PROCEDURE AddTimeZoneConversionEntry (ZoneAsText,ZoneAsImapOffset : STRING);  // e.g. 'UTC','+0000'
BEGIN
  IF TimeZoneTable.IndexOfName(ZoneAsText) > -1 THEN
  BEGIN
    log (log_fatal,'Duplicate entry in timezone converion ('+ZoneAsText+'). Check your cfg file.');
    halt(1);
  END;
  TimeZoneTable.add (ZoneAsText + '=' + ZoneAsImapOffset);
END;


//-----------------------------------------------------------------------------


CONSTRUCTOR TMailbox.Create (Flgs : STRING);
BEGIN
  Flags := Flgs;
  Flgs := LowerCase (Flags);
  Noinferiors   := FALSE;
  Noselect      := FALSE;
  Marked        := FALSE;
  Unmarked      := FALSE;
  HasChildren   := FALSE;

  Noinferiors := (Pos ('\noinferiors', Flgs) > 0);
  Noselect    := (Pos ('\noselect', Flgs) > 0);
  Marked      := (Pos ('\marked', Flgs) > 0);
  Unmarked    := (Pos ('\unmarked', Flgs) > 0);
  HasChildren := (Pos ('\haschildren', Flgs) > 0);

END;


INITIALIZATION
  TimeZoneTable := TStringList.Create;
  TimeZoneTable.Sorted := true;
  {$IFDEF Win32}
  // not needed for Unix
  IF WSAStartup($101, InitData) = 0 THEN
    Initialized := TRUE
  ELSE BEGIN
    WriteLn ('Unable to initialize winsock');
    HALT(2);
  END;
  {$ENDIF}
FINALIZATION
{$IFDEF Win32}
  IF Initialized THEN WSACleanup;
{$ENDIF}
  TimeZoneTable.Free;
END.
