\
\ Example of interfacing with a Dallas Semiconductor DS1804
\ NV trimmer potentiometer
\
\ All timings are respected with Fosc=40MHz. However, we have absolutely
\ no margin, so please try to set extra delays before reporting bugs. Also,
\ user is responsible for respecting startup and eeprom write time as
\ indicated in the datasheet.
\

LATC 3 bit /CSn
LATC 4 bit U/D
LATC 5 bit /INC

: select ( -- ) /INC bit-clr /CSn bit-clr ; inline
: deselect ( -- ) /CSn bit-set ; inline
: save-to-eeprom ( -- ) select /INC bit-set nop nop nop nop deselect ;
: cycle ( -- ) select /INC bit-set /INC bit-clr deselect ;
: up ( -- ) U/D bit-set cycle ;
: down ( -- ) U/D bit-clr cycle ;

: zero ( -- ) 100 cfor down cnext ;
: max ( -- ) 100 cfor up cnext ;

: init-ds1804 ( -- ) deselect TRISC 3 bit-clr TRISC 4 bit-clr TRISC 5 bit-clr ;
