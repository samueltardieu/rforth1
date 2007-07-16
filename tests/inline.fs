: empty ; inline
: foo dup ; no-inline
: bar dup foo recurse empty ; no-inline
: main bar bar dup ;
