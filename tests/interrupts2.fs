: handle-high ;
: handle-low ;

: high
  save-everything-high
  handle-high
  restore-everything-high
; high-interrupt

: low
  save-everything-low
  handle-low
  restore-everything-low
; low-interrupt

: main ;
