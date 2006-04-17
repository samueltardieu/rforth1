needs lib/tty-rs232.fs

create pattern1 0b0111 c, 0b1101 c, 0b1011 c, 0b1110 c,
create pattern2 0b1001 c, 0b1010 c, 0b0110 c, 0b0101 c,
create pattern3 0b0111 c, 0b1001 c, 0b1101 c, 0b1010 c,
                0b1011 c, 0b0110 c, 0b1110 c, 0b0101 c,

variable pattern-base
cvariable pattern-length
cvariable pattern-index
variable delay

: advance-pattern ( -- )
  pattern-index c@ 1+ dup pattern-length c@ >= if drop 0 then
  pattern-index c! ;

: next-output ( -- u ) pattern-base @ pattern-index c@ + c@ advance-pattern ;

: set-next-output ( -- )
  next-output 2* LATA c!
;

: choose-pattern ( addr length -- )
  pattern-length c! pattern-base ! 0 pattern-index c!
  set-next-output
;

: p1 ( -- ) pattern1 4 choose-pattern ;
: p2 ( -- ) pattern2 4 choose-pattern ;
: p3 ( -- ) pattern3 8 choose-pattern ;

: init-ports ( -- ) 6 ADCON1 c! 0xe1 TRISA c! ;

: wait ( -- ) delay @ TMR0L ! TMR0IF bit-clr begin TMR0IF bit-set? until ;

: steps ( -- )
  begin
    begin
      key? while
      key
      dup [char] + = if 0x200 delay +! else
      dup [char] - = if 0x200 delay -! else
      dup 27 = if drop exit then then then drop
    repeat
    set-next-output wait
  again ;

: free-wheels 0x1e LATA c! ;

: init-timer ( -- ) 0x82 T0CON c! ;

: step ( -- )
  key
  dup [char] 1 = if drop p1 exit then
  dup [char] 2 = if drop p2 exit then
  dup [char] 3 = if drop p3 exit then
  dup [char] S = if drop set-next-output exit then
  dup [char] T = if drop 0xbdc delay ! steps exit then
  dup [char] F = if drop free-wheels exit then
  drop
;

: mainloop ( -- ) begin step again ;

: main ( -- ) p1 init-ports init-timer mainloop ;