needs lib/tty-rs232.fs

: handle-key ( n/w -- )
  switchw
    0 casew ." 000"
    1 casew ." 111"
    2 casew ." 222"
    defaultw ." 3333"
  endswitchw
; inw

: main ( -- ) begin read4 handle-key again ;