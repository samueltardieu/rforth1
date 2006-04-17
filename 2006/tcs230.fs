needs lib/tty.fs

LATB 6 bit S0
LATB 7 bit S1
LATC 0 bit S2
LATC 1 bit S3
LATA 0 bit /OE
PORTC 2 bit IN

: init-tcs ( -- ) $fe TRISA c! $3f TRISB c! $bc TRISC c! LATC 2 bit-clr ;

cvariable color-mode
color-mode 0 bit DOCLEAR
color-mode 1 bit DORED
color-mode 2 bit DOGREEN
color-mode 3 bit DOBLUE

: select ( -- ) /OE bit-clr ;
: deselect ( -- ) /OE bit-set ;
: clear ( -- ) S2 bit-set S3 bit-clr ;
: blue ( -- ) S2 bit-clr S3 bit-set ;
: green ( -- ) S2 bit-set S3 bit-set ;
: red ( -- ) S2 bit-clr S3 bit-clr ;
: mode-down ( -- ) S0 bit-clr S1 bit-clr ;
: mode-2% ( -- ) S0 bit-clr S1 bit-set ;
: mode-20% ( -- ) S0 bit-set S1 bit-clr ;
: mode-100% ( -- ) S0 bit-set S1 bit-set ;

: wait-for-down ( -- ) begin IN bit-clr? until ; inline
: wait-for-up ( -- ) begin IN bit-set? until ; inline

: wait-for-ccp ( -- ) begin CCP1IF bit-set? until CCP1IF bit-clr ; inline
: reset-ccp ( -- ) CCP1IF bit-clr 0 TMR1L ! ;

: count-pulses ( n -- time )
  \ Turn the module on and Wait for the first pulse
  select wait-for-up reset-ccp wait-for-ccp CCPR1L @ swap
  \ Wait for the required number of cycles
  cfor wait-for-ccp cnext
  \ Compute the time difference and shut down module
  deselect CCPR1L @ swap - ;

: init-vars ( -- ) 15 color-mode c! ;
: init-ccp ( -- ) $05 CCP1CON c! $81 T1CON c! $00 T3CON c! ;

: help ( -- )
  ." 0: off  1: 100% [default] 2: 20%  3: 2%" cr
  ." C: clear  R: red  G: green  B: blue  A: all [default]" cr
  ." W: 1 pulse  X: 10 pulses  C: 100 pulses" cr
;

: prompt ( -- ) ." ?>" ;

: .measure ( n -- ) count-pulses . cr ;

: do-measure ( n -- )
  DOCLEAR bit-set? if ." CLEAR: " dup clear .measure then
  DORED bit-set? if ." RED:   " dup red .measure then
  DOGREEN bit-set? if ." GREEN: " dup green .measure then
  DOBLUE bit-set? if ." BLUE:  " dup blue .measure then
  drop
;

: handle-key ( c -- )
  dup [char] 0 = if drop mode-down exit then
  dup [char] 1 = if drop mode-100% exit then
  dup [char] 2 = if drop mode-20% exit then
  dup [char] 3 = if drop mode-2% exit then
  dup [char] C = if drop 1 color-mode c! exit then
  dup [char] R = if drop 2 color-mode c! exit then
  dup [char] G = if drop 4 color-mode c! exit then
  dup [char] B = if drop 8 color-mode c! exit then
  dup [char] A = if drop 15 color-mode c! exit then
  dup [char] W = if drop 1 do-measure exit then
  dup [char] X = if drop 10 do-measure exit then
  dup [char] C = if drop 100 do-measure exit then
  drop help
;

: mainloop ( -- ) begin prompt key dup emit cr handle-key again ;

: main ( -- ) init-tcs mode-100% init-vars init-ccp help mainloop ;
