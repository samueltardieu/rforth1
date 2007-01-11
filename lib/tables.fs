: table-addr! ( a -- ) 0 TBLPTRU c! 1>2 TBLPTRH c! TBLPTRL c! CFGS bit-clr ;

code flash-addr!
    INDF0 7 ,a bcf  \ Clear high bit -- usable addresses are 0x0000 to 0x7FFF
    EEPGD ,a bsf
    table-addr! bra
;code

: tablec@+ ( -- c ) tblrd*+ TABLAT c@ ; inline

: eepromc*+ ( -- c ) RD bit-set begin RD bit-clr? until tablec@+ ;
: flashc*+ ( -- c ) tablec@+ ; inline

: flashc@ ( a -- c ) flash-addr! flashc*+ ;
: flash@ ( a -- c ) flash-addr! flashc*+ flashc*+ 2>1 ;

: eeprom-addr! ( a -- ) w> EEADR c! EEPGD bit-clr CFGS bit-clr ; inw

: eepromc@ ( a -- c ) eeprom-addr! RD bit-set EEDATA c@ ;

: eeprom@ ( a -- u ) dup eepromc@ swap 1+ eepromc@ 2>1 ;

: eepromc! ( c a -- )
  eeprom-addr! EEDATA c!
  WREN bit-set 0x55 EECON2 c! 0xAA EECON2 c! WR bit-set
  begin WR bit-clr? until
  WREN bit-clr
  EEIF bit-clr
;

: eeprom! ( u a -- ) >r 1>2 r@ 1+ eepromc! r> eepromc! ;

: flash-operate ( -- )
  EEPGD bit-set WREN bit-set 0x55 EECON2 c! 0xAA EECON2 c! WR bit-set
  nop nop
;

: flash-erase ( addr -- ) flash-addr! FREE bit-set flash-operate ;

: flash-block ( addr -- addr+8 )
  8 cfor dup c@ TABLAT c! tblwt+* 1+ cnext flash-operate ;

: flash-write ( bufferaddr flashaddr -- )
  flash-erase tblrd*- 8 cfor flash-block cnext drop ;
