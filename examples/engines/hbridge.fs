needs lib/tty.fs

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

: recede-pattern ( -- )
  pattern-index c@ 1- dup 0< if drop pattern-length c@ 1- then 
  pattern-index c! ;

: next-output ( -- u ) pattern-base @ pattern-index c@ + c@ 
DIR bit-set? if recede-pattern else advance-pattern then ;

: set-next-output ( -- )
  next-output 2* LATA c!
;

: choose-pattern ( addr length -- )
  pattern-length c! pattern-base ! 0 pattern-index c!
  set-next-output
;

: free-wheel 0 LATA c! ;

: p1 ( -- ) pattern1 4 choose-pattern ;
: p2 ( -- ) pattern2 4 choose-pattern ;
: p3 ( -- ) pattern3 8 choose-pattern ;
