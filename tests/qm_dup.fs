: foobar ( x -- x*x ) ?dup if dup * then ;

: ?2dup ?dup ?dup ;

: loop begin foobar again ;

: main ?2dup loop ;