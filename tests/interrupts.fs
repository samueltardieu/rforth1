variable a
cvariable w_save

: high 1 a ! ; high-interrupt fast
: low w> w_save c! 2 a !  w_save c@ >w ; low-interrupt
: main ;
